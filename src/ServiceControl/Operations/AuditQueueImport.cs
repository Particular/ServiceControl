namespace ServiceControl.Operations
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Metrics;
    using NServiceBus;
    using NServiceBus.Logging;
    using NServiceBus.ObjectBuilder;
    using NServiceBus.Satellites;
    using NServiceBus.Transports;
    using NServiceBus.Unicast;
    using NServiceBus.Unicast.Transport;
    using Raven.Client;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Contracts.Operations;
    using ServiceControl.MessageAuditing;
    using Timer = Metrics.Timer;

    public class AuditQueueImport : IAdvancedSatellite, IDisposable
    {
        private const int BATCH_SIZE = 100;
        private const int RampUpConcurrencyMagicNumber = 5; //How many batches before we ramp up?

        private static readonly ILog Logger = LogManager.GetLogger(typeof(AuditQueueImport));
        private readonly IBuilder builder;
        private readonly CountDownEvent countDownEvent = new CountDownEvent();
        private readonly CriticalError criticalError;
        private readonly IEnrichImportedMessages[] enrichers;
        private readonly ISendMessages forwarder;
        private readonly Histogram histogram = Metric.Histogram("Messages pending saving", Unit.Items);
        private readonly object lockObj = new object();
        private readonly LoggingSettings loggingSettings;
        private readonly BlockingCollection<TransportMessage> messages = new BlockingCollection<TransportMessage>();

        private readonly RunningTasksTracker runningTasksTracker = new RunningTasksTracker();
        private readonly Settings settings;
        private readonly IDocumentStore store;
        private readonly Timer timer = Metric.Timer("Audit messages", Unit.Requests);
        private readonly Timer timer2 = Metric.Timer("Messages pending saving", Unit.Requests);
        private SatelliteImportFailuresHandler satelliteImportFailuresHandler;
        private volatile bool stopping;

        public AuditQueueImport(IBuilder builder, ISendMessages forwarder, IDocumentStore store, CriticalError criticalError, LoggingSettings loggingSettings, Settings settings)
        {
            this.builder = builder;
            this.forwarder = forwarder;
            this.store = store;

            this.criticalError = criticalError;
            this.loggingSettings = loggingSettings;
            this.settings = settings;

            enrichers = builder.BuildAll<IEnrichImportedMessages>().ToArray();
        }

        public bool Handle(TransportMessage message)
        {
            using (timer.NewContext())
            {
                InnerHandle(message);
            }

            return true;
        }

        public void Start()
        {
            stopping = false;

            TryStartNewBatchImporter();

            runningTasksTracker.Add(Task.Run(() =>
            {
                do
                {
                    histogram.Update(messages.Count);
                    Thread.Sleep(1000);
                } while (!stopping);
            }));

            if (!TerminateIfForwardingIsEnabledButQueueNotWritable())
            {
                Logger.Info($"Audit import is now started, feeding audit messages from: {InputAddress}");
            }
        }


        public void Stop()
        {
            stopping = true;

            Task.WaitAll(runningTasksTracker.Active());
        }

        public Address InputAddress => settings.AuditQueue;

        public bool Disabled => false;

        public Action<TransportReceiver> GetReceiverCustomization()
        {
            satelliteImportFailuresHandler = new SatelliteImportFailuresHandler(builder.Build<IDocumentStore>(),
                Path.Combine(loggingSettings.LogPath, @"FailedImports\Audit"), tm => new FailedAuditImport
                {
                    Message = tm
                },
                criticalError);

            return receiver => { receiver.FailureManager = satelliteImportFailuresHandler; };
        }

        public void Dispose()
        {
            satelliteImportFailuresHandler?.Dispose();
        }

        private void InnerHandle(TransportMessage message)
        {
            messages.Add(message);

            if (settings.ForwardAuditMessages)
            {
                TransportMessageCleaner.CleanForForwarding(message);
                forwarder.Send(message, new SendOptions(settings.AuditLogQueue));
            }
        }

        private ProcessedMessage ConvertToSaveMessage(TransportMessage message)
        {
            var receivedMessage = new ImportSuccessfullyProcessedMessage(message);

            foreach (var enricher in enrichers)
            {
                enricher.Enrich(receivedMessage);
            }

            var auditMessage = new ProcessedMessage(receivedMessage)
            {
                // We do this so Raven does not spend time assigning a hilo key
                Id = $"ProcessedMessages/{Guid.NewGuid()}"
            };
            return auditMessage;
        }

        private bool TryStartNewBatchImporter()
        {
            lock (lockObj)
            {
                if (countDownEvent.CurrentCount > settings.MaximumConcurrencyLevel)
                {
                    return false;
                }
                countDownEvent.Add();
            }

            if (stopping)
            {
                return true;
            }

            runningTasksTracker.Add(Task.Factory
                .StartNew(BatchImporter, CancellationToken.None)
                .ContinueWith(task =>
                {
                    if (task.Exception != null)
                    {
                        task.Exception.Handle(ex =>
                        {
                            Logger.Error("Error processing message.", ex);
                            return true;
                        });
                        runningTasksTracker.Remove(task);
                    }
                }));
            return true;
        }

        private void BatchImporter()
        {
            try
            {
                var moreMessages = 0;

                do
                {
                    if (moreMessages > RampUpConcurrencyMagicNumber)
                    {
                        if (TryStartNewBatchImporter())
                        {
                            Logger.Info("We have too many messages, starting another batch importer");
                            moreMessages = 0; //Reset to 0 so we only ramp up once per BatchImporter
                        }
                    }

                    moreMessages++;

                    TransportMessage message;

                    if (!messages.TryTake(out message, 5))
                    {
                        moreMessages = 0;
                        continue;
                    }

                    using (var context = timer2.NewContext())
                    {
                        var cnt = 0;
                        using (var bulkInsert = store.BulkInsert())
                        {
                            bulkInsert.Store(ConvertToSaveMessage(message));
                            cnt++;

                            for (var i = 0; i < BATCH_SIZE - 1; i++)
                            {
                                if (!messages.TryTake(out message, 5))
                                {
                                    moreMessages = 0;
                                    break;
                                }

                                bulkInsert.Store(ConvertToSaveMessage(message));
                                cnt++;
                            }
                        }

                        context.TrackUserValue(cnt.ToString());
                    }
                } while (StopBatchImporter(moreMessages));
            }
            finally
            {
                countDownEvent.Decrement();
                Logger.Info("Decommissioning batch importer");
            }
        }

        private bool StopBatchImporter(int moreMessages)
        {
            if (stopping)
            {
                return false;
            }

            if (countDownEvent.CurrentCount == 1)
            {
                return true;
            }

            if (moreMessages > 0)
            {
                return true;
            }

            return false;
        }

        private bool TerminateIfForwardingIsEnabledButQueueNotWritable()
        {
            if (!settings.ForwardAuditMessages)
            {
                return false;
            }

            try
            {
                //Send a message to test the forwarding queue
                var testMessage = new TransportMessage(Guid.Empty.ToString("N"), new Dictionary<string, string>());
                forwarder.Send(testMessage, new SendOptions(settings.AuditLogQueue));
                return false;
            }
            catch (Exception messageForwardingException)
            {
                criticalError.Raise("Audit Import cannot start", messageForwardingException);
                return true;
            }
        }

        private class RunningTasksTracker
        {
            private readonly List<Task> tasks = new List<Task>();

            public void Add(Task task)
            {
                lock (tasks)
                {
                    tasks.Add(task);
                }
            }

            public void Remove(Task task)
            {
                lock (tasks)
                {
                    tasks.Remove(task);
                }
            }

            public Task[] Active()
            {
                lock (tasks)
                {
                    return tasks.Where(x => !x.IsCompleted).ToArray();
                }
            }
        }

        private class CountDownEvent
        {
            private volatile int counter;

            public int CurrentCount => counter;

            public void Add()
            {
#pragma warning disable 420
                Interlocked.Increment(ref counter);
#pragma warning restore 420
            }

            public void Decrement()
            {
#pragma warning disable 420
                Interlocked.Decrement(ref counter);
#pragma warning restore 420
            }
        }
    }
}