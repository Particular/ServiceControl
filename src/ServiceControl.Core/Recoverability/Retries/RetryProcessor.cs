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

        bool executedStartupTasks;

        public RetryProcessor(IDocumentStore store, NoopDequeuer noopDequeuer, ReturnToSenderDequeuer returnToSenderDequeuer, ISendMessages sender, IBodyStorage bodyStorage, RetryDocumentManager retryDocumentManager)
        {
            executor = new PeriodicExecutor(Process, TimeSpan.FromSeconds(30));
            this.store = store;
            this.noopDequeuer = noopDequeuer;
            this.returnToSenderDequeuer = returnToSenderDequeuer;
            this.sender = sender;
            this.bodyStorage = bodyStorage;
            this.retryDocumentManager = retryDocumentManager;
        }

        public void Dispose()
        {
            Stop();
        }

        public void Start()
        {
            executor.Start(false);
        }

        public void Stop()
        {
            executor.Stop();
        }

        void Process(PeriodicExecutor e)
        {
            if (!executedStartupTasks)
            {
                AdoptOrphanedBatches();
                executedStartupTasks = true;
            }

            string batchesProcessed = null;
            do
            {
                using (var session = store.OpenSession())
                {
                    batchesProcessed = ProcessBatches(session, batchesProcessed);
                    session.SaveChanges();
                }
            } while (batchesProcessed != null && !e.IsCancellationRequested);
        }

        void AdoptOrphanedBatches()
        {
            using (var session = store.OpenSession())
            {
                var batches = session.Query<RetryBatch>()
                    .Customize(q => q.WaitForNonStaleResultsAsOfNow())
                    .Where(b => b.Status == RetryBatchStatus.MarkingDocuments)
                    .ToArray();

                foreach (var batch in batches)
                {
                    var retryFailureIds = session.Query<MessageFailureRetry>()
                        .Customize(q => q.WaitForNonStaleResultsAsOfNow())
                        .Where(r => r.RetryBatchId == batch.Id)
                        .Select(r => r.Id)
                        .ToArray();

                    retryDocumentManager.MoveBatchToStaging(batch.Id, retryFailureIds);
                }
            }
        }

        string ProcessBatches(IDocumentSession session, string batchId)
        {
            RetryBatch forwardingBatch;

            if (batchId == null)
            {
                forwardingBatch = session
                    .Query<RetryBatch>()
                    .Customize(c => c.WaitForNonStaleResultsAsOfNow())
                    .SingleOrDefault(x => x.Status == RetryBatchStatus.Forwarding);
            }
            else
            {
                forwardingBatch = session.Load<RetryBatch>(batchId);
            }

            if (forwardingBatch != null)
            {
                Forward(forwardingBatch, session);
                return null;
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

                return stagingBatch.Id;
            }

            return null;
        }

        void Stage(RetryBatch batch, MessageFailureHistory[] messages)
        {
            //Clear Staging Queue
            noopDequeuer.Run();

            foreach (var message in messages)
            {
                StageMessage(message);
            }

            batch.Status = RetryBatchStatus.Forwarding;

            Log.InfoFormat("Retry batch {0} staged", batch.Id);
        }

        static byte[] ReadFully(Stream input)
        {
            var buffer = new byte[16*1024];
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

        void StageMessage(MessageFailureHistory failedMessage)
        {
            failedMessage.Status = FailedMessageStatus.RetryIssued;

            var attempt = failedMessage.ProcessingAttempts.Last();

            var originalHeaders = attempt.Headers;

            var headersToRetryWith = originalHeaders.Where(kv => !KeysToRemoveWhenRetryingAMessage.Contains(kv.Key))
                .ToDictionary(kv => kv.Key, kv => kv.Value);

            headersToRetryWith["ServiceControl.TargetEndpointAddress"] = attempt.FailureDetails.AddressOfFailingEndpoint;
            headersToRetryWith["ServiceControl.Retry.UniqueMessageId"] = failedMessage.UniqueMessageId;

            using (var stream = bodyStorage.Fetch(attempt.MessageId))
            {
                var transportMessage = new TransportMessage(failedMessage.Id, headersToRetryWith)
                {
                    Body = ReadFully(stream),
                    CorrelationId = attempt.CorrelationId,
                    Recoverable = attempt.Recoverable,
                    MessageIntent = (MessageIntentEnum) Enum.Parse(typeof(MessageIntentEnum), attempt.MessageIntent, true)
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

        readonly IBodyStorage bodyStorage;
        readonly NoopDequeuer noopDequeuer;
        readonly ReturnToSenderDequeuer returnToSenderDequeuer;
        readonly ISendMessages sender;
        PeriodicExecutor executor;
        IDocumentStore store;
        RetryDocumentManager retryDocumentManager;
    }
}