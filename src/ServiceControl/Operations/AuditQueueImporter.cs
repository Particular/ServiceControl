namespace ServiceControl.Operations
{
    using System;
    using System.Collections.Generic;
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
        }

        public IBus Bus { get; set; }
        public KnownEndpointsCache KnownEndpointsCache { get; set; }

        public void Start()
        {
            if (!enabled)
            {
                return;
            }

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

            Logger.InfoFormat("MSMQ Audit import is now started, feeding audit messages from: {0}", Settings.AuditQueue);

            queue.PeekCompleted += QueueOnPeekCompleted;

            CallPeekWithExceptionHandling(() => queue.BeginPeek());
        }

        public void Stop()
        {
            if (!enabled)
            {
                return;
            }

            stopping = true;

            queue.PeekCompleted -= QueueOnPeekCompleted;

            stopResetEvent.WaitOne();

            performanceCounters.Dispose();

            queue.Dispose();

            runResetEvent.Dispose();
            stopResetEvent.Dispose();
        }

        void QueueOnPeekCompleted(object sender, PeekCompletedEventArgs args)
        {
            stopResetEvent.Reset();

            CallPeekWithExceptionHandling(() => queue.EndPeek(args.AsyncResult));

            Task.Factory.StartNew(Run, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

            runResetEvent.WaitOne();

            CallPeekWithExceptionHandling(() => queue.BeginPeek());

            stopResetEvent.Set();
        }

        void CallPeekWithExceptionHandling(Action action)
        {
            try
            {
                action();
            }
            catch (MessageQueueException messageQueueException)
            {
                Logger.Fatal("Failed to peek", messageQueueException);
            }
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
            var id = endpointDetails.Name + endpointDetails.Machine;

            if (KnownEndpointsCache.TryAdd(id))
            {
                Bus.SendLocal(new RegisterEndpoint
                {
                    Endpoint = endpointDetails,
                    DetectedAt = DateTime.UtcNow
                });
            }
        }

        void Run()
        {
            try
            {
                var moreMessages = true;

                do
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
                            for (var idx = 0; idx < BatchSize; idx++)
                            {
                                Message message;

                                try
                                {
                                    message = queue.Receive(receiveTimeout, msmqTransaction);
                                    performanceCounters.MessageDequeued();
                                }
                                catch (MessageQueueException mqe)
                                {
                                    if (mqe.MessageQueueErrorCode == MessageQueueErrorCode.IOTimeout)
                                    {
                                        moreMessages = false;
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
                                RegisterNewEndpointIfNeeded(importSuccessfullyProcessedMessage);

                                var auditMessage = new ProcessedMessage(importSuccessfullyProcessedMessage);
                                bulkInsert.Store(auditMessage);
                                performanceCounters.MessageProcessed();
                            }
                        }

                        msmqTransaction.Commit();
                    }
                } while (moreMessages && !stopping);
            }
            finally
            {
                runResetEvent.Set();
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
        readonly bool enabled;
        readonly MsmqAuditImporterPerformanceCounters performanceCounters = new MsmqAuditImporterPerformanceCounters();
        readonly TimeSpan receiveTimeout = TimeSpan.FromSeconds(1);
        readonly AutoResetEvent runResetEvent = new AutoResetEvent(false);
        readonly ManualResetEvent stopResetEvent = new ManualResetEvent(true);
        readonly IDocumentStore store;
        List<IEnrichImportedMessages> enrichers;
        MessageQueue queue;
        volatile bool stopping;
    }
}