namespace ServiceControl.Recoverability.Retries
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using NServiceBus;
    using NServiceBus.Logging;
    using NServiceBus.Transports;
    using Raven.Client;
    using ServiceControl.MessageFailures;
    using ServiceControl.Operations.BodyStorage;
    using ServiceControl.Shell.Api;

    class RetryProcessor : IWantToRunWhenBusStartsAndStops, IDisposable
    {
        PeriodicExecutor executor;
        IDocumentStore store;
        readonly NoopDequeuer noopDequeuer;
        readonly ReturnToSenderDequeuer returnToSenderDequeuer;
        readonly ISendMessages sender;
        readonly IBodyStorage bodyStorage;

        public RetryProcessor(IDocumentStore store, NoopDequeuer noopDequeuer, ReturnToSenderDequeuer returnToSenderDequeuer, ISendMessages sender, IBodyStorage bodyStorage)
        {
            executor = new PeriodicExecutor(Process, TimeSpan.FromSeconds(30));
            this.store = store;
            this.noopDequeuer = noopDequeuer;
            this.returnToSenderDequeuer = returnToSenderDequeuer;
            this.sender = sender;
            this.bodyStorage = bodyStorage;
        }

        private void Process(PeriodicExecutor e)
        {
            bool batchesProcessed;
            do
            {
                using (var session = store.OpenSession())
                {
                    batchesProcessed = ProcessBatches(session);
                    session.SaveChanges();
                }
            } while (batchesProcessed && !e.IsCancellationRequested);
        }

        bool ProcessBatches(IDocumentSession session)
        {
            var forwardingBatch = session
                .Query<RetryBatch>()
                .SingleOrDefault(x => x.Status == RetryBatchStatus.Forwarding);
            if (forwardingBatch != null)
            {
                Forward(forwardingBatch, session);
                return true;
            }

            var stagingBatch = session
                .Query<RetryBatch>()
                .Customize(x => x.Include<RetryBatch, MessageFailureRetry>(b => b.FailureRetries))
                .FirstOrDefault(x => x.Status == RetryBatchStatus.Staging);
            if (stagingBatch != null)
            {
                var messageIds = session.Load<MessageFailureRetry>(stagingBatch.FailureRetries)
                    .Where(x => x != null && x.RetryBatchId == stagingBatch.Id)
                    .Select(x => x.FailureMessageId);

                var messages = session.Load<MessageFailureHistory>(messageIds);

                Stage(stagingBatch, messages);

                foreach (var failureRetry in stagingBatch.FailureRetries)
                {
                    session.Advanced.DocumentStore.DatabaseCommands.Delete(failureRetry, null);
                }

                return true;
            }

            return false;
        }

        void Stage(RetryBatch batch, MessageFailureHistory[] messages)
        {
            //Clear Staging Queue
            noopDequeuer.Run();

            foreach (var message in messages)
            {
                PutMessageInStagingQueue(message);
            }

            batch.Status = RetryBatchStatus.Forwarding;

            // TODO: Issue a message indicating the status change?
            Log.InfoFormat("Retry batch {0} staged", batch.Id);
        }

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

        void PutMessageInStagingQueue(MessageFailureHistory failedMessage)
        {
            if (failedMessage.Status != FailedMessageStatus.Unresolved)
            {
                // We only retry messages that are unresolved
                return;
            }

            var attempt = failedMessage.ProcessingAttempts.Last();

            var originalHeaders = attempt.Headers;

            var headersToRetryWith = originalHeaders.Where(kv => !KeysToRemoveWhenRetryingAMessage.Contains(kv.Key))
                .ToDictionary(kv => kv.Key, kv => kv.Value);

            headersToRetryWith["ServiceControl.TargetEndpointAddress"] = attempt.FailureDetails.AddressOfFailingEndpoint;

            using (var stream = bodyStorage.Fetch(attempt.MessageId))
            {
                var transportMessage = new TransportMessage(failedMessage.Id, headersToRetryWith)
                {
                    Body = ReadFully(stream),
                    CorrelationId = attempt.CorrelationId,
                    Recoverable = attempt.Recoverable,
                    MessageIntent = (MessageIntentEnum)Enum.Parse(typeof(MessageIntentEnum), attempt.MessageIntent, true),
                };

                if (!String.IsNullOrWhiteSpace(attempt.ReplyToAddress))
                {
                    transportMessage.ReplyToAddress = Address.Parse(attempt.ReplyToAddress);
                }

                sender.Send(transportMessage, AdvancedDequeuer.Address);
            }
        }

        void Forward(RetryBatch batch, IDocumentSession session)
        {
            // Process the staging queue
            returnToSenderDequeuer.Run();

            session.Delete(batch);

            Log.InfoFormat("Retry batch {0} done", batch.Id);
        }


        public void Start()
        {
            executor.Start(false);
        }

        public void Stop()
        {
            executor.Stop();
        }

        public void Dispose()
        {
            Stop();
        }

        static ILog Log = LogManager.GetLogger(typeof(RetryProcessor));
    }
}
