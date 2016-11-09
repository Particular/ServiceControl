namespace ServiceControl.Recoverability
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using NServiceBus;
    using NServiceBus.Logging;
    using NServiceBus.Transports;
    using NServiceBus.Unicast;
    using Raven.Client;
    using ServiceControl.MessageFailures;
    using ServiceControl.MessageRedirects;
    using ServiceControl.Operations.BodyStorage;

    class RetryProcessor
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

        public RetryProcessor(IBodyStorage bodyStorage, ISendMessages sender, IBus bus, ReturnToSenderDequeuer returnToSender, RetryOperationManager retryOperationManager)
        {
            this.bodyStorage = bodyStorage;
            this.sender = sender;
            this.bus = bus;
            this.returnToSender = returnToSender;
            this.retryOperationManager = retryOperationManager;
        }

        public bool ProcessBatches(IDocumentSession session)
        {
            return ForwardCurrentBatch(session) || MoveStagedBatchesToForwardingBatch(session);
        }

        private bool MoveStagedBatchesToForwardingBatch(IDocumentSession session)
        {
            isRecoveringFromPrematureShutdown = false;

            var stagingBatch = session.Query<RetryBatch>()
                .Customize(q => q.Include<RetryBatch, FailedMessageRetry>(b => b.FailureRetries))
                .FirstOrDefault(b => b.Status == RetryBatchStatus.Staging);

            if (stagingBatch != null)
            {
                redirects = MessageRedirectsCollection.GetOrCreate(session);
                var stagedMessages = Stage(stagingBatch, session);
                var skippedMessages = stagingBatch.InitialBatchSize - stagedMessages;
                retryOperationManager.Skip(stagingBatch.RequestId, stagingBatch.RetryType, skippedMessages);

                if ( stagedMessages > 0)
                {
                    session.Store(new RetryBatchNowForwarding
                    {
                        RetryBatchId = stagingBatch.Id
                    }, RetryBatchNowForwarding.Id);
                }

                return true;
            }

            return false;
        }

        private bool ForwardCurrentBatch(IDocumentSession session)
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

            return false;
        }

        void Forward(RetryBatch forwardingBatch, IDocumentSession session)
        {
            var messageCount = forwardingBatch.FailureRetries.Count;

            retryOperationManager.Forwarding(forwardingBatch.RequestId, forwardingBatch.RetryType);

            if (isRecoveringFromPrematureShutdown)
            {
                returnToSender.Run(IsPartOfStagedBatch(forwardingBatch.StagingId));
                retryOperationManager.ForwardedBatch(forwardingBatch.RequestId, forwardingBatch.RetryType, forwardingBatch.InitialBatchSize);
            }
            else 
            {
                
                returnToSender.Run(IsPartOfStagedBatch(forwardingBatch.StagingId), messageCount);
                retryOperationManager.ForwardedBatch(forwardingBatch.RequestId, forwardingBatch.RetryType, messageCount);
            }

            session.Delete(forwardingBatch);
            
            Log.InfoFormat("Retry batch {0} done", forwardingBatch.Id);
        }

        static Predicate<TransportMessage> IsPartOfStagedBatch(string stagingId)
        {
            return m =>
            {
                var messageStagingId = m.Headers["ServiceControl.Retry.StagingId"];
                return messageStagingId == stagingId;
            };
        }

        int Stage(RetryBatch stagingBatch, IDocumentSession session)
        {
            var stagingId = Guid.NewGuid().ToString();

            var matchingFailures = session.Load<FailedMessageRetry>(stagingBatch.FailureRetries)
                .Where(r => r != null && r.RetryBatchId == stagingBatch.Id)
                .ToArray();

            var messageIds = matchingFailures.Select(x => x.FailedMessageId).ToArray();

            if (!messageIds.Any())
            {
                Log.InfoFormat("Retry batch {0} cancelled as all matching unresolved messages are already marked for retry as part of another batch", stagingBatch.Id);
                session.Delete(stagingBatch);
                return 0;
            }

            var messages = session.Load<FailedMessage>(messageIds)
                .Where(m => m != null)
                .ToArray();

            foreach (var message in messages)
            {
                StageMessage(message, stagingId);
            }

            bus.Publish<MessagesSubmittedForRetry>(m =>
            {
                m.FailedMessageIds = messages.Select(x => x.UniqueMessageId).ToArray();
                m.Context = stagingBatch.Context;
            });

            var msgLookup = messages.ToLookup(x => x.Id);

            stagingBatch.Status = RetryBatchStatus.Forwarding;
            stagingBatch.StagingId = stagingId;
            stagingBatch.FailureRetries = matchingFailures.Where(x => msgLookup[x.FailedMessageId].Any()).Select(x => x.Id).ToArray();

            Log.InfoFormat("Retry batch {0} staged {1} messages", stagingBatch.Id, messages.Length);
            return messages.Length;
        }

        void StageMessage(FailedMessage message, string stagingId)
        {
            message.Status = FailedMessageStatus.RetryIssued;

            var attempt = message.ProcessingAttempts.Last();

            var headersToRetryWith = attempt.Headers.Where(kv => !KeysToRemoveWhenRetryingAMessage.Contains(kv.Key))
                .ToDictionary(kv => kv.Key, kv => kv.Value);

            var addressOfFailingEndpoint = attempt.FailureDetails.AddressOfFailingEndpoint;

            var redirect = redirects[addressOfFailingEndpoint];

            if (redirect != null)
            {
                addressOfFailingEndpoint = redirect.ToPhysicalAddress;
            }

            headersToRetryWith["ServiceControl.TargetEndpointAddress"] = addressOfFailingEndpoint;
            headersToRetryWith["ServiceControl.Retry.UniqueMessageId"] = message.UniqueMessageId;
            headersToRetryWith["ServiceControl.Retry.StagingId"] = stagingId;
            if (!string.IsNullOrWhiteSpace(attempt.ReplyToAddress))
            {
                headersToRetryWith[Headers.ReplyToAddress] = attempt.ReplyToAddress;
            }

            var transportMessage = new TransportMessage(message.Id, headersToRetryWith)
            {
                Recoverable = attempt.Recoverable,
                MessageIntent = attempt.MessageIntent
            };
            if (attempt.CorrelationId != null)
            {
                transportMessage.CorrelationId = attempt.CorrelationId;
            }

            Stream stream;
            if (bodyStorage.TryFetch(attempt.MessageId, out stream))
            {
                using (stream)
                {
                    transportMessage.Body = ReadFully(stream);
                }
            }

            sender.Send(transportMessage, new SendOptions(returnToSender.InputAddress));
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
        IBus bus;
        ReturnToSenderDequeuer returnToSender;
        RetryOperationManager retryOperationManager;
        private MessageRedirectsCollection redirects;
        bool isRecoveringFromPrematureShutdown = true;
    }
}