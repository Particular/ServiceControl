namespace ServiceControl.Recoverability
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Extensibility;
    using NServiceBus.Logging;
    using NServiceBus.Routing;
    using NServiceBus.Support;
    using NServiceBus.Transport;
    using Raven.Client;
    using ServiceControl.Infrastructure.DomainEvents;
    using ServiceControl.MessageFailures;
    using ServiceControl.MessageRedirects;

    class RetryProcessor
    {
        static readonly List<string> KeysToRemoveWhenRetryingAMessage = new List<string>
        {
            "NServiceBus.Retries",
            "NServiceBus.FailedQ",
            "NServiceBus.TimeOfFailure",
            "NServiceBus.ExceptionInfo.ExceptionType",
            "NServiceBus.ExceptionInfo.AuditMessage",
            "NServiceBus.ExceptionInfo.Source",
            "NServiceBus.ExceptionInfo.StackTrace"
        };

        static ILog Log = LogManager.GetLogger(typeof(RetryProcessor));

        public RetryProcessor(IDispatchMessages sender, IDomainEvents domainEvents, ReturnToSenderDequeuer returnToSender, RetryingManager retryingManager)
        {
            this.sender = sender;
            this.returnToSender = returnToSender;
            this.retryingManager = retryingManager;
            this.domainEvents = domainEvents;
            corruptedReplyToHeaderStrategy = new CorruptedReplyToHeaderStrategy(RuntimeEnvironment.MachineName);
        }

        public async Task<bool> ProcessBatches(IAsyncDocumentSession session, CancellationToken cancellationToken)
        {
            return await ForwardCurrentBatch(session, cancellationToken) || await MoveStagedBatchesToForwardingBatch(session);
        }

        private async Task<bool> MoveStagedBatchesToForwardingBatch(IAsyncDocumentSession session)
        {
            isRecoveringFromPrematureShutdown = false;

            var stagingBatch = await session.Query<RetryBatch>()
                .Customize(q => q.Include<RetryBatch, FailedMessageRetry>(b => b.FailureRetries))
                .FirstOrDefaultAsync(b => b.Status == RetryBatchStatus.Staging)
                .ConfigureAwait(false);

            if (stagingBatch != null)
            {
                redirects = await MessageRedirectsCollection.GetOrCreate(session).ConfigureAwait(false);
                var stagedMessages = await Stage(stagingBatch, session).ConfigureAwait(false);
                var skippedMessages = stagingBatch.InitialBatchSize - stagedMessages;
                await retryingManager.Skip(stagingBatch.RequestId, stagingBatch.RetryType, skippedMessages)
                    .ConfigureAwait(false);

                if ( stagedMessages > 0)
                {
                    await session.StoreAsync(new RetryBatchNowForwarding
                    {
                        RetryBatchId = stagingBatch.Id
                    }, RetryBatchNowForwarding.Id)
                    .ConfigureAwait(false);
                }

                return true;
            }

            return false;
        }

        private async Task<bool> ForwardCurrentBatch(IAsyncDocumentSession session, CancellationToken cancellationToken)
        {
            Log.Debug("Looking for batch to forward");

            var nowForwarding = await session.Include<RetryBatchNowForwarding, RetryBatch>(r => r.RetryBatchId)
                .LoadAsync<RetryBatchNowForwarding>(RetryBatchNowForwarding.Id)
                .ConfigureAwait(false);

            if (nowForwarding != null)
            {
                Log.DebugFormat("Loading batch {0} for forwarding", nowForwarding.RetryBatchId);

                var forwardingBatch = await session.LoadAsync<RetryBatch>(nowForwarding.RetryBatchId, cancellationToken).ConfigureAwait(false);

                if (forwardingBatch != null)
                {
                    Log.InfoFormat("Found batch {0}. Forwarding...", forwardingBatch.Id);
                    await Forward(forwardingBatch, session, cancellationToken)
                        .ConfigureAwait(false);
                    Log.DebugFormat("Retry batch {0} forwarded.", forwardingBatch.Id);
                }
                else
                {
                    Log.WarnFormat("Could not find retry batch {0} to forward", nowForwarding.RetryBatchId);
                }

                Log.Debug("Removing Forwarding record");

                session.Delete(nowForwarding);
                return true;
            }

            Log.Debug("No batch found to forward");

            return false;
        }

        async Task Forward(RetryBatch forwardingBatch, IAsyncDocumentSession session, CancellationToken cancellationToken)
        {
            var messageCount = forwardingBatch.FailureRetries.Count;

            Log.InfoFormat("Forwarding batch {0} with {1} messages", forwardingBatch.Id, messageCount);
            await retryingManager.Forwarding(forwardingBatch.RequestId, forwardingBatch.RetryType)
                .ConfigureAwait(false);

            if (isRecoveringFromPrematureShutdown)
            {
                Log.Warn("Recovering from premature shutdown. Starting forwarder in timeout mode");
                await returnToSender.Run(IsPartOfStagedBatch(forwardingBatch.StagingId), cancellationToken)
                    .ConfigureAwait(false);
                await retryingManager.ForwardedBatch(forwardingBatch.RequestId, forwardingBatch.RetryType, forwardingBatch.InitialBatchSize)
                    .ConfigureAwait(false);
            }
            else
            {
                Log.DebugFormat("Starting forwarder in counting mode with {0} messages", messageCount);
                await returnToSender.Run(IsPartOfStagedBatch(forwardingBatch.StagingId), cancellationToken, messageCount)
                    .ConfigureAwait(false);
                await retryingManager.ForwardedBatch(forwardingBatch.RequestId, forwardingBatch.RetryType, messageCount)
                    .ConfigureAwait(false);
            }

            session.Delete(forwardingBatch);

            Log.InfoFormat("Retry batch {0} done", forwardingBatch.Id);
        }

        static Predicate<MessageContext> IsPartOfStagedBatch(string stagingId)
        {
            return m =>
            {
                var messageStagingId = m.Headers["ServiceControl.Retry.StagingId"];
                return messageStagingId == stagingId;
            };
        }

        async Task<int> Stage(RetryBatch stagingBatch, IAsyncDocumentSession session)
        {
            var stagingId = Guid.NewGuid().ToString();
            var failedMessageRetryDocs = await session.LoadAsync<FailedMessageRetry>(stagingBatch.FailureRetries).ConfigureAwait(false);
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
                Log.Info($"Retry batch {stagingBatch.Id} cancelled as all matching unresolved messages are already marked for retry as part of another batch");
                session.Delete(stagingBatch);
                return 0;
            }

            var failedMessagesDocs = await session.LoadAsync<FailedMessage>(messageIds).ConfigureAwait(false);
            var messages = failedMessagesDocs
                .Where(m => m != null)
                .ToArray();

            Log.DebugFormat("Staging {0} messages for Retry Batch {1} with staging attempt Id {2}", messages.Length, stagingBatch.Id, stagingId);

            await Task.WhenAll(messages.Select(m => StageMessage(m, stagingId)).ToArray());

            if (stagingBatch.RetryType != RetryType.FailureGroup) //FailureGroup published on completion of entire group
            {
                var failedIds = messages.Select(x => x.UniqueMessageId).ToArray();
                await domainEvents.Raise(new MessagesSubmittedForRetry
                {
                    FailedMessageIds = failedIds,
                    NumberOfFailedMessages = failedIds.Length,
                    Context = stagingBatch.Context
                }).ConfigureAwait(false);
            }

            var msgLookup = messages.ToLookup(x => x.Id);

            stagingBatch.Status = RetryBatchStatus.Forwarding;
            stagingBatch.StagingId = stagingId;
            stagingBatch.FailureRetries = matchingFailures.Where(x => msgLookup[x.FailedMessageId].Any()).Select(x => x.Id).ToArray();
            Log.DebugFormat("Retry batch {0} staged with Staging Id {1} and {2} matching failure retries", stagingBatch.Id, stagingBatch.StagingId, stagingBatch.FailureRetries.Count);
            Log.InfoFormat("Retry batch {0} staged {1} messages", stagingBatch.Id, messages.Length);
            return messages.Length;
        }

        Task StageMessage(FailedMessage message, string stagingId)
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
            headersToRetryWith["ServiceControl.Retry.Attempt.MessageId"] = attempt.MessageId;

            corruptedReplyToHeaderStrategy.FixCorruptedReplyToHeader(headersToRetryWith);

            var transportMessage = new OutgoingMessage(message.Id, headersToRetryWith, emptyBody);
            transportMessage.Headers[Headers.MessageIntent] = attempt.MessageIntent.ToString();
            if (attempt.Recoverable)
            {
                transportMessage.Headers[Headers.NonDurableMessage] = true.ToString();
            }

            if (attempt.CorrelationId != null)
            {
                transportMessage.Headers[Headers.CorrelationId] = attempt.CorrelationId;
            }

            return sender.Dispatch(new TransportOperations(new TransportOperation(transportMessage, new UnicastAddressTag(returnToSender.InputAddress))), transaction, contextBag);
        }

        byte[] emptyBody = new byte[0];
        TransportTransaction transaction = new TransportTransaction();
        ContextBag contextBag = new ContextBag();
        IDispatchMessages sender;
        IDomainEvents domainEvents;
        ReturnToSenderDequeuer returnToSender;
        RetryingManager retryingManager;
        MessageRedirectsCollection redirects;
        bool isRecoveringFromPrematureShutdown = true;
        CorruptedReplyToHeaderStrategy corruptedReplyToHeaderStrategy;
    }
}