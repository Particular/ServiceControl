namespace ServiceControl.Recoverability
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using NLog;
    using NServiceBus;
    using NServiceBus.Logging;
    using NServiceBus.Support;
    using NServiceBus.Transports;
    using NServiceBus.Unicast;
    using Raven.Client;
    using ServiceControl.MessageFailures;
    using ServiceControl.MessageRedirects;
    using LogManager = NServiceBus.Logging.LogManager;

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

        public RetryProcessor(ISendMessages sender, IBus bus, ReturnToSenderDequeuer returnToSender, RetryOperationManager retryOperationManager)
        {
            this.sender = sender;
            this.bus = bus;
            this.returnToSender = returnToSender;
            this.retryOperationManager = retryOperationManager;
            corruptedReplyToHeaderStrategy = new CorruptedReplyToHeaderStrategy(RuntimeEnvironment.MachineName);
        }

        public bool ProcessBatches(IDocumentSession session, CancellationToken cancellationToken)
        {
            return ForwardCurrentBatch(session, cancellationToken) || MoveStagedBatchesToForwardingBatch(session);
        }

        private bool MoveStagedBatchesToForwardingBatch(IDocumentSession session)
        {
            isRecoveringFromPrematureShutdown = false;

            var stagingBatch = session.Query<RetryBatch>()
                .Customize(q => q.Include<RetryBatch, FailedMessageRetry>(b => b.FailureRetries))
                .FirstOrDefault(b => b.Status == RetryBatchStatus.Staging);

            if (stagingBatch != null)
            {
                LogBatch("Now Staging", stagingBatch.Id);

                redirects = MessageRedirectsCollection.GetOrCreate(session);
                var stagedMessages = Stage(stagingBatch, session);

                LogBatch($"{stagedMessages} messages staged", stagingBatch.Id);

                var skippedMessages = stagingBatch.InitialBatchSize - stagedMessages;

                LogBatch($"{skippedMessages} messages skipped", stagingBatch.Id);

                retryOperationManager.Skip(stagingBatch.RequestId, stagingBatch.RetryType, skippedMessages);

                if ( stagedMessages > 0)
                {
                    LogBatch("Creating RetryBatchNowForwarding", stagingBatch.Id);

                    session.Store(new RetryBatchNowForwarding
                    {
                        RetryBatchId = stagingBatch.Id
                    }, RetryBatchNowForwarding.Id);
                }

                return true;
            }

            Log.Debug("No RetryBatch to stage");

            return false;
        }

        private static Logger retryBatchLogger = NLog.LogManager.GetLogger("RetryBatch");

        private static void LogBatch(string message, string retryBatchId)
        {
            var evt = new LogEventInfo(NLog.LogLevel.Info, string.Empty, message);
            evt.Properties.Add("RetryBatchId",retryBatchId);
            retryBatchLogger.Log(evt);
        }

        private bool ForwardCurrentBatch(IDocumentSession session, CancellationToken cancellationToken)
        {
            var nowForwarding = session.Include<RetryBatchNowForwarding, RetryBatch>(r => r.RetryBatchId)
                .Load<RetryBatchNowForwarding>(RetryBatchNowForwarding.Id);

            if (nowForwarding != null)
            {
                LogBatch("Now Forwarding", nowForwarding.RetryBatchId);

                var forwardingBatch = session.Load<RetryBatch>(nowForwarding.RetryBatchId);

                if (forwardingBatch != null)
                {
                    Forward(forwardingBatch, session, cancellationToken);
                }
                else
                {
                    LogBatch("RetryBatch not found", nowForwarding.RetryBatchId);
                }

                LogBatch("Removing RetryBatchNowForwarding", nowForwarding.RetryBatchId);

                session.Delete(nowForwarding);
                return true;
            }

            Log.Debug("No RetryBatch to forward");

            return false;
        }

        void Forward(RetryBatch forwardingBatch, IDocumentSession session, CancellationToken cancellationToken)
        {
            var messageCount = forwardingBatch.FailureRetries.Count;

            LogBatch($"Forwarding Started for {messageCount} messages", forwardingBatch.Id);

            retryOperationManager.Forwarding(forwardingBatch.RequestId, forwardingBatch.RetryType);

            if (isRecoveringFromPrematureShutdown)
            {
                LogBatch("Recovering from premature shutdown", forwardingBatch.Id);
                returnToSender.Run(IsPartOfStagedBatch(forwardingBatch.StagingId), cancellationToken, msg => LogBatch(msg, forwardingBatch.Id));
                retryOperationManager.ForwardedBatch(forwardingBatch.RequestId, forwardingBatch.RetryType, forwardingBatch.InitialBatchSize);
            }
            else
            {

                returnToSender.Run(IsPartOfStagedBatch(forwardingBatch.StagingId), cancellationToken, msg => LogBatch(msg, forwardingBatch.Id), messageCount);
                retryOperationManager.ForwardedBatch(forwardingBatch.RequestId, forwardingBatch.RetryType, messageCount);
            }

            LogBatch("Deleting RetryBatch", forwardingBatch.Id);

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

            LogBatch($"StagingId {stagingId} assigned", stagingBatch.Id);

            var failedMessageRetryDocs = session.Load<FailedMessageRetry>(stagingBatch.FailureRetries);
            var matchingFailures = failedMessageRetryDocs
                .Where(r => r != null && r.RetryBatchId == stagingBatch.Id)
                .ToArray();

            foreach (var failedMessageRetry in failedMessageRetryDocs)
            {
                if (failedMessageRetry != null)
                {
                    session.Advanced.Evict(failedMessageRetry);
                }
            }

            var messageIds = matchingFailures.Select(x => x.FailedMessageId).ToArray();

            if (!messageIds.Any())
            {
                LogBatch("Cancelled. All messages marked for retry", stagingBatch.Id);
                Log.Info($"Retry batch {stagingBatch.Id} cancelled as all matching unresolved messages are already marked for retry as part of another batch");

                LogBatch("Deleting StagingBatch", stagingBatch.Id);
                session.Delete(stagingBatch);
                return 0;
            }

            var messages = session.Load<FailedMessage>(messageIds)
                .Where(m => m != null)
                .ToArray();

            LogBatch($"Staging {messages.Length} messages in parallel", stagingBatch.Id);

            Parallel.ForEach(messages, message => StageMessage(message, stagingId, stagingBatch.Id));

            if (stagingBatch.RetryType != RetryType.FailureGroup) //FailureGroup published on completion of entire group
            {
                LogBatch("Publishing MessagesSubmittedForRetry", stagingBatch.Id);

                bus.Publish<MessagesSubmittedForRetry>(m =>
                {
                    var failedIds = messages.Select(x => x.UniqueMessageId).ToArray();
                    m.FailedMessageIds = failedIds;
                    m.NumberOfFailedMessages = failedIds.Length;
                    m.Context = stagingBatch.Context;
                });
            }

            var msgLookup = messages.ToLookup(x => x.Id);

            LogBatch("Status set to Forwarding", stagingBatch.Id);

            stagingBatch.Status = RetryBatchStatus.Forwarding;
            stagingBatch.StagingId = stagingId;
            stagingBatch.FailureRetries = matchingFailures.Where(x => msgLookup[x.FailedMessageId].Any()).Select(x => x.Id).ToArray();

            Log.InfoFormat("Retry batch {0} staged {1} messages", stagingBatch.Id, messages.Length);
            return messages.Length;
        }

        void StageMessage(FailedMessage message, string stagingId, string stagingBatchId)
        {
            LogBatch($"[{message.UniqueMessageId}] BEGIN Staging", stagingBatchId);
            LogBatch($"[{message.UniqueMessageId}] Status set to RetryIssued", stagingBatchId);
            message.Status = FailedMessageStatus.RetryIssued;

            var attempt = message.ProcessingAttempts.Last();

            var headersToRetryWith = attempt.Headers.Where(kv => !KeysToRemoveWhenRetryingAMessage.Contains(kv.Key))
                .ToDictionary(kv => kv.Key, kv => kv.Value);

            var addressOfFailingEndpoint = attempt.FailureDetails.AddressOfFailingEndpoint;

            LogBatch($"[{message.UniqueMessageId}] retry address {addressOfFailingEndpoint}", stagingBatchId);

            var redirect = redirects[addressOfFailingEndpoint];

            if (redirect != null)
            {
                addressOfFailingEndpoint = redirect.ToPhysicalAddress;
                LogBatch($"[{message.UniqueMessageId}] redirected to address {addressOfFailingEndpoint}", stagingBatchId);
            }

            headersToRetryWith["ServiceControl.TargetEndpointAddress"] = addressOfFailingEndpoint;
            headersToRetryWith["ServiceControl.Retry.UniqueMessageId"] = message.UniqueMessageId;
            headersToRetryWith["ServiceControl.Retry.StagingId"] = stagingId;
            headersToRetryWith["ServiceControl.Retry.Attempt.MessageId"] = attempt.MessageId;

            corruptedReplyToHeaderStrategy.FixCorruptedReplyToHeader(headersToRetryWith);

            var transportMessage = new TransportMessage(message.Id, headersToRetryWith)
            {
                Recoverable = attempt.Recoverable,
                MessageIntent = attempt.MessageIntent
            };
            if (attempt.CorrelationId != null)
            {
                transportMessage.CorrelationId = attempt.CorrelationId;
            }

            LogBatch($"[{message.UniqueMessageId}] sending TransportMessage", stagingBatchId);

            sender.Send(transportMessage, new SendOptions(returnToSender.InputAddress));

            LogBatch($"[{message.UniqueMessageId}] END Staging", stagingBatchId);
        }

        ISendMessages sender;
        IBus bus;
        ReturnToSenderDequeuer returnToSender;
        RetryOperationManager retryOperationManager;
        private MessageRedirectsCollection redirects;
        bool isRecoveringFromPrematureShutdown = true;
        private CorruptedReplyToHeaderStrategy corruptedReplyToHeaderStrategy;
    }
}