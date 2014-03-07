namespace ServiceControl.Operations
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Messaging;
    using System.Threading;
    using System.Threading.Tasks;
    using Contracts.Operations;
    using EndpointControl.Handlers;
    using EndpointControl.InternalMessages;
    using MessageAuditing;
    using MessageFailures.InternalMessages;
    using NServiceBus;
    using NServiceBus.Logging;
    using NServiceBus.ObjectBuilder;
    using NServiceBus.Transports;
    using NServiceBus.Transports.Msmq;
    using NServiceBus.Unicast;
    using Raven.Abstractions.Data;
    using Raven.Client;
    using ServiceBus.Management.Infrastructure.Settings;

    internal class AuditQueueImporter : IWantToRunWhenBusStartsAndStops
    {
        public AuditQueueImporter(IDocumentStore store, IBuilder builder, IDequeueMessages receiver)
        {
            this.store = store;
            this.builder = builder;
            enabled = receiver is MsmqDequeueStrategy;

            importFailuresHandler = new SatelliteImportFailuresHandler(store,
                Path.Combine(Settings.LogPath, @"FailedImports\Audit"), tm => new FailedAuditImport
                {
                    Message = tm,
                });
        }

        public IBus Bus { get; set; }
        public KnownEndpointsCache KnownEndpointsCache { get; set; }
        public UnicastBus UnicastBus { get; set; }

        public void Start()
        {
            if (!enabled)
            {
                return;
            }

            performanceCounters.Initialize();

            queuePeeker = new MessageQueue(MsmqUtilities.GetFullPath(Settings.AuditQueue), QueueAccessMode.Peek);
            queuePeeker.MessageReadPropertyFilter.ClearAll();
            queuePeeker.PeekCompleted += QueueOnPeekCompleted;

            enrichers = builder.BuildAll<IEnrichImportedMessages>().ToList();

            Logger.InfoFormat("MSMQ Audit import is now started, feeding audit messages from: {0}", Settings.AuditQueue);

            countDownEvent.Idle += OnIdle;

            Logger.Debug("Ready to BeginPeek");
            queuePeeker.BeginPeek();
        }

        public void Stop()
        {
            if (!enabled)
            {
                return;
            }

            stopping = true;

            queuePeeker.PeekCompleted -= QueueOnPeekCompleted;

            stopResetEvent.Wait();

            performanceCounters.Dispose();

            queuePeeker.Dispose();

            stopResetEvent.Dispose();
        }

        static MessageQueue CreateReceiver()
        {
            var queue = new MessageQueue(MsmqUtilities.GetFullPath(Settings.AuditQueue), QueueAccessMode.Receive);

            var messageReadPropertyFilter = new MessagePropertyFilter
            {
                Body = true,
                TimeToBeReceived = true,
                Recoverable = true,
                Id = true,
                ResponseQueue = true,
                CorrelationId = true,
                Extension = true,
                AppSpecific = true
            };

            queue.MessageReadPropertyFilter = messageReadPropertyFilter;

            return queue;
        }

        void OnIdle(object sender, EventArgs eventArgs)
        {
            stopResetEvent.Set();

            if (stopping)
            {
                return;
            }

            Logger.Debug("Ready to BeginPeek again");
            queuePeeker.BeginPeek();
        }

        void QueueOnPeekCompleted(object sender, PeekCompletedEventArgs args)
        {
            stopResetEvent.Reset();

            TryStartNewBatchImporter();
        }

        bool TryStartNewBatchImporter()
        {
            lock (lockObj)
            {
                if (countDownEvent.CurrentCount > UnicastBus.Transport.MaximumConcurrencyLevel)
                {
                    return false;
                }

                countDownEvent.Add();
            }

            Task.Factory
                .StartNew(BatchImporter, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default)
                .ContinueWith(task =>
                {
                    task.Exception.Handle(ex =>
                    {
                        Logger.Error("Error processing message.", ex);
                        return true;
                    });
                }, TaskContinuationOptions.OnlyOnFaulted);

            return true;
        }

        void SendRegisterSuccessfulRetryIfNeeded(ImportSuccessfullyProcessedMessage message)
        {
            string retryId;

            if (!message.PhysicalMessage.Headers.TryGetValue("ServiceControl.RetryId", out retryId))
            {
                return;
            }

            Bus.SendLocal(new RegisterSuccessfulRetry
            {
                FailedMessageId = message.UniqueMessageId,
                RetryId = Guid.Parse(retryId)
            });
        }

        void RegisterNewEndpointIfNeeded(ImportMessage message)
        {
            TryAddEndpoint(EndpointDetails.SendingEndpoint(message.PhysicalMessage.Headers));
            TryAddEndpoint(EndpointDetails.ReceivingEndpoint(message.PhysicalMessage.Headers));
        }

        void TryAddEndpoint(EndpointDetails endpointDetails)
        {
            var id = endpointDetails.Name + endpointDetails.Host;

            if (KnownEndpointsCache.TryAdd(id))
            {
                Bus.SendLocal(new RegisterEndpoint
                {
                    Endpoint = endpointDetails,
                    DetectedAt = DateTime.UtcNow
                });
            }
        }

        void BatchImporter()
        {
            try
            {
                Logger.Debug("Batch job started");

                var moreMessages = 0;

                using (var queueReceiver = CreateReceiver())
                {
                    do
                    {
                        if (moreMessages > RampUpConcurrencyMagicNumber)
                        {
                            if (TryStartNewBatchImporter())
                            {
                                Logger.Debug("We have too many messages, starting another batch importer");

                                moreMessages = 0; //Reset to 0 so we only ramp up once per BatchImporter
                            }
                        }

                        moreMessages++;

                        using (var msmqTransaction = new MessageQueueTransaction())
                        {
                            msmqTransaction.Begin();
                            using (
                                var bulkInsert =
                                    store.BulkInsert(options:
                                        new BulkInsertOptions {CheckForUpdates = true})
                                )
                            {
                                for (var idx = 0; idx < BatchSize; idx++)
                                {
                                    Message message;

                                    try
                                    {
                                        message = queueReceiver.Receive(receiveTimeout, msmqTransaction);
                                        performanceCounters.MessageDequeued();
                                    }
                                    catch (MessageQueueException mqe)
                                    {
                                        if (mqe.MessageQueueErrorCode == MessageQueueErrorCode.IOTimeout)
                                        {
                                            moreMessages = 0;
                                            break;
                                        }

                                        importFailuresHandler.FailedToReceive(mqe);
                                        throw;
                                    }

                                    var transportMessage = ConvertMessage(message);

                                    try
                                    {
                                        var importSuccessfullyProcessedMessage =
                                            new ImportSuccessfullyProcessedMessage(transportMessage);

                                        foreach (var enricher in enrichers)
                                        {
                                            enricher.Enrich(importSuccessfullyProcessedMessage);
                                        }

                                        SendRegisterSuccessfulRetryIfNeeded(importSuccessfullyProcessedMessage);
                                        RegisterNewEndpointIfNeeded(importSuccessfullyProcessedMessage);

                                        var auditMessage = new ProcessedMessage(importSuccessfullyProcessedMessage);
                                        bulkInsert.Store(auditMessage);
                                        performanceCounters.MessageProcessed();
                                    }
                                    catch (Exception ex)
                                    {
                                        importFailuresHandler.ProcessingAlwaysFailsForMessage(transportMessage, ex);
                                        throw;
                                    }
                                }
                            }

                            msmqTransaction.Commit();
                        }
                    } while (moreMessages > 0 && !stopping);
                }

                Logger.Debug("Stopping batch importer");
            }
            finally
            {
                countDownEvent.Decrement();
            }
        }

        TransportMessage ConvertMessage(Message message)
        {
            try
            {
                return MsmqUtilities.Convert(message);
            }
            catch (Exception ex)
            {
                importFailuresHandler.FailedToReceive(ex);
                throw;
            }
        }

        const int RampUpConcurrencyMagicNumber = 5; //How many batches before we ramp up?
        const int BatchSize = 100;

        static readonly ILog Logger = LogManager.GetLogger(typeof(AuditQueueImporter));

        readonly IBuilder builder;
        readonly CountDownEvent countDownEvent = new CountDownEvent();
        readonly bool enabled;
        readonly SatelliteImportFailuresHandler importFailuresHandler;
        readonly object lockObj = new object();
        readonly MsmqAuditImporterPerformanceCounters performanceCounters = new MsmqAuditImporterPerformanceCounters();
        readonly TimeSpan receiveTimeout = TimeSpan.FromSeconds(1);
        readonly ManualResetEventSlim stopResetEvent = new ManualResetEventSlim(true);
        readonly IDocumentStore store;

        List<IEnrichImportedMessages> enrichers;
        MessageQueue queuePeeker;
        volatile bool stopping;

        class CountDownEvent
        {
            public int CurrentCount
            {
                get { return counter; }
            }

            public event EventHandler Idle;

            public void Add()
            {
#pragma warning disable 420
                Interlocked.Increment(ref counter);
#pragma warning restore 420
            }

            public void Decrement()
            {
#pragma warning disable 420
                if (Interlocked.Decrement(ref counter) == 0)
#pragma warning restore 420
                {
                    Idle(this, EventArgs.Empty);
                }
            }

            volatile int counter;
        }
    }
}