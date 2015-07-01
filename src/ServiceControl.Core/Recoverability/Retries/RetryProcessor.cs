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
    using ServiceControl.InternalContracts.Messages.Recoverability;
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

        public RetryProcessor(IDocumentStore store, NoopDequeuer noopDequeuer, ReturnToSenderDequeuer returnToSenderDequeuer, ISendMessages sender, IBodyStorage bodyStorage, IBus bus)
        {
            executor = new PeriodicExecutor(Process, TimeSpan.FromSeconds(30));
            this.store = store;
            this.noopDequeuer = noopDequeuer;
            this.returnToSenderDequeuer = returnToSenderDequeuer;
            this.sender = sender;
            this.bodyStorage = bodyStorage;
            this.bus = bus;
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
                session.Store(new RetryBatchNowForwarding { RetryBatchId = stagingBatch.Id }, RetryBatchNowForwarding.Id);
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
                StageMessage(message);
            }

            bus.Publish<MessagesSubmittedForRetry>(m => m.FailedMessageIds = messages.Select(x => x.UniqueMessageId).ToArray());

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
        IBus bus;
    }

    public class RetryBatchNowForwarding
    {
        public const string Id = "RetryBatches/NowForwarding";
        public string RetryBatchId { get; set; }
    }
}