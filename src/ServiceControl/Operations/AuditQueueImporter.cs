namespace ServiceControl.Operations
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Messaging;
    using System.Threading;
    using System.Threading.Tasks;
    using Contracts.Operations;
    using MessageAuditing;
    using MessageFailures.InternalMessages;
    using NServiceBus;
    using NServiceBus.Logging;
    using NServiceBus.ObjectBuilder;
    using NServiceBus.Transports.Msmq;
    using Raven.Abstractions.Data;
    using Raven.Client;
    using ServiceBus.Management.Infrastructure.Settings;

    internal class AuditQueueImporter : IWantToRunWhenBusStartsAndStops
    {
        public AuditQueueImporter(IDocumentStore store, IBuilder builder)
        {
            this.store = store;
            this.builder = builder;
        }

        public IBus Bus { get; set; }

        public void Start()
        {
            performanceCounters.Initialize();

            queue = new MessageQueue(MsmqUtilities.GetFullPath(Settings.AuditQueue), QueueAccessMode.Receive);

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

            enrichers = builder.BuildAll<IEnrichImportedMessages>().ToList();

            var token = tokenSource.Token;

            for (var i = 0; i < 20; i++)
            {
                runningTasks.Add(Task.Factory.StartNew(Run, token, token, TaskCreationOptions.LongRunning,
                    TaskScheduler.Default));
            }
        }

        public void Stop()
        {
            tokenSource.Cancel();

            Task.WaitAll(runningTasks.ToArray());

            performanceCounters.Dispose();

            tokenSource.Dispose();
            queue.Dispose();
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

        void Run(object obj)
        {
            var cancellationToken = (CancellationToken) obj;

            while (!cancellationToken.IsCancellationRequested)
            {
                using (var msmqTransaction = new MessageQueueTransaction())
                {
                    msmqTransaction.Begin();
                    using (
                        var bulkInsert =
                            store.BulkInsert(options:
                                new BulkInsertOptions {CheckForUpdates = true})
                        )
                    {
                        for (var i = 0; i < BatchSize; i++)
                        {
                            Message message;

                            try
                            {
                                message = queue.Receive(timeout, msmqTransaction);
                                performanceCounters.MessageDequeued();
                            }
                            catch (MessageQueueException mqe)
                            {
                                if (mqe.MessageQueueErrorCode == MessageQueueErrorCode.IOTimeout)
                                {
                                    break;
                                }
                                throw; //TODO: What to do here?
                            }

                            var transportMessage = ConvertMessage(message);
                            var importSuccessfullyProcessedMessage =
                                new ImportSuccessfullyProcessedMessage(transportMessage);

                            foreach (var enricher in enrichers)
                            {
                                enricher.Enrich(importSuccessfullyProcessedMessage);
                            }

                            SendRegisterSuccessfulRetryIfNeeded(importSuccessfullyProcessedMessage);

                            var auditMessage = new ProcessedMessage(importSuccessfullyProcessedMessage);
                            bulkInsert.Store(auditMessage);
                            performanceCounters.MessageProcessed();
                        }
                    }

                    msmqTransaction.Commit();
                }
            }
        }

        static TransportMessage ConvertMessage(Message message)
        {
            try
            {
                return MsmqUtilities.Convert(message);
            }
            catch (Exception ex)
            {
                Logger.Error("Error in converting message to TransportMessage.", ex);

                return new TransportMessage(Guid.Empty.ToString(), null);
            }
        }

        const int BatchSize = 100;

        static readonly ILog Logger = LogManager.GetLogger(typeof(AuditQueueImporter));
        readonly IBuilder builder;
        readonly MsmqAuditImporterPerformanceCounters performanceCounters = new MsmqAuditImporterPerformanceCounters();
        readonly List<Task> runningTasks = new List<Task>();
        readonly IDocumentStore store;
        readonly TimeSpan timeout = TimeSpan.FromSeconds(1);
        readonly CancellationTokenSource tokenSource = new CancellationTokenSource();
        List<IEnrichImportedMessages> enrichers;
        MessageQueue queue;
    }
}