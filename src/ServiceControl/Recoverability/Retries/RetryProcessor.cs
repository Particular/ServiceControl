namespace ServiceControl.Recoverability
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using NServiceBus;
    using NServiceBus.Logging;
    using NServiceBus.Transports;
    using Raven.Client;
    using ServiceControl.Infrastructure.RavenDB.Expiration;
    using ServiceControl.MessageFailures;
    using ServiceControl.Operations.BodyStorage;

    class RetryProcessor : IWantToRunWhenBusStartsAndStops, IDisposable
    {
        static readonly List<string> KeysToRemoveWhenRetryingAMessage = new List<string>
        {
            Headers.Retries,
            "NServiceBus.FailedQ",
            "NServiceBus.TimeOfFailure",
            "NServiceBus.ExceptionInfo.ExceptionType",
            "NServiceBus.ExceptionInfo.AuditMessage",
            "NServiceBus.ExceptionInfo.Source",
            "NServiceBus.ExceptionInfo.StackTrace"
        };

        static ILog Log = LogManager.GetLogger(typeof(RetryProcessor));

        public RetryProcessor(IBodyStorage bodyStorage, ISendMessages sender, IDocumentStore store, IBus bus, NoopDequeuer stagingQueueCleaner, ReturnToSenderDequeuer returnToSender)
        {
            executor = new PeriodicExecutor(Process
                , TimeSpan.FromSeconds(30));
            this.bodyStorage = bodyStorage;
            this.sender = sender;
            this.store = store;
            this.bus = bus;
            this.stagingQueueCleaner = stagingQueueCleaner;
            this.returnToSender = returnToSender;
        }

        public void Start()
        {
            executor.Start(false);
        }

        public void Stop()
        {
            executor.Stop(CancellationToken.None);
        }

        public void Dispose()
        {
            Stop();
        }

        void Process()
        {
            bool batchesProcessed;
            do
            {
                using (var session = store.OpenSession())
                {
                    batchesProcessed = ProcessBatches(session);
                    session.SaveChanges();
                }
            } while (batchesProcessed);
        }

        bool ProcessBatches(IDocumentSession session)
        {
            var nowForwarding = session.Include<RetryBatchNowForwarding, RetryBatch>(r => r.RetryBatchId)
                .Load<RetryBatchNowForwarding>(RetryBatchNowForwarding.Id);

            if (nowForwarding != null)
            {
                var forwardingBatch = session.Load<RetryBatch>(nowForwarding.RetryBatchId);

                if (forwardingBatch != null)
                {
                    Forward(forwardingBatch, session);
                }

                session.Delete(nowForwarding);
                return true;
            }

            var stagingBatch = session.Query<RetryBatch>()
                .Customize(q => q.Include<RetryBatch, FailedMessageRetry>(b => b.FailureRetries))
                .FirstOrDefault(b => b.Status == RetryBatchStatus.Staging);

            if (stagingBatch != null)
            {
                Stage(stagingBatch, session);
                session.Store(new RetryBatchNowForwarding { RetryBatchId = stagingBatch.Id }, RetryBatchNowForwarding.Id);
                return true;
            }

            return false;
        }

        void Forward(RetryBatch forwardingBatch, IDocumentSession session)
        {
            returnToSender.Run();

            session.Delete(forwardingBatch);

            Log.InfoFormat("Retry batch {0} done");
        }

        void Stage(RetryBatch stagingBatch, IDocumentSession session)
        {
            stagingQueueCleaner.Run();

            var messageIds = session.Load<FailedMessageRetry>(stagingBatch.FailureRetries)
                .Where(r => r != null && r.RetryBatchId == stagingBatch.Id)
                .Select(r => r.FailedMessageId);

            var messages = session.Load<FailedMessage>(messageIds);

            foreach (var message in messages)
            {
                StageMessage(message);
            }

            // TODO: Publish a message on the bus stating that batch has been sent

            stagingBatch.Status = RetryBatchStatus.Forwarding;

            Log.InfoFormat("Retry batch {0} staged {1} messages", stagingBatch.Id, messages.Count());
        }

        void StageMessage(FailedMessage message)
        {
            message.Status = FailedMessageStatus.RetryIssued;

            var attempt = message.ProcessingAttempts.Last();

            var headersToRetryWith = attempt.Headers.Where(kv => !KeysToRemoveWhenRetryingAMessage.Contains(kv.Key))
                .ToDictionary(kv => kv.Key, kv => kv.Value);

            headersToRetryWith["ServiceControl.TargetEndpointAddress"] = attempt.FailureDetails.AddressOfFailingEndpoint;
            headersToRetryWith["ServiceControl.Retry.UniqueMessageId"] = message.UniqueMessageId;

            using (var stream = bodyStorage.Fetch(attempt.MessageId))
            {
                var transportMessage = new TransportMessage(message.Id, headersToRetryWith)
                {
                    Body = ReadFully(stream),
                    CorrelationId = attempt.CorrelationId,
                    Recoverable = attempt.Recoverable,
                    MessageIntent = attempt.MessageIntent
                };

                if (!String.IsNullOrWhiteSpace(attempt.ReplyToAddress))
                {
                    transportMessage.ReplyToAddress = Address.Parse(attempt.ReplyToAddress);
                }

                sender.Send(transportMessage, AdvancedDequeuer.Address);
            }
        }

        static byte[] ReadFully(Stream input)
        {
            var buffer = new byte[16 * 1024];
            using (var ms = new MemoryStream())
            {
                int read;
                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    ms.Write(buffer, 0, read);
                }
                return ms.ToArray();
            }
        }

        IBodyStorage bodyStorage;
        ISendMessages sender;
        PeriodicExecutor executor;
        IDocumentStore store;
        IBus bus;
        NoopDequeuer stagingQueueCleaner;
        ReturnToSenderDequeuer returnToSender;
    }
}