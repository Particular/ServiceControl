namespace ServiceControl.Recoverability
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Infrastructure.DomainEvents;
    using MessageFailures;
    using MessageRedirects;
    using NServiceBus;
    using NServiceBus.Extensibility;
    using NServiceBus.Logging;
    using NServiceBus.Routing;
    using NServiceBus.Support;
    using NServiceBus.Transport;
    using Raven.Abstractions.Data;
    using Raven.Abstractions.Exceptions;
    using Raven.Client;

    class RetryProcessor
    {
        protected internal const int MaxStagingAttempts = 5;

        public RetryProcessor(IDocumentStore store, IDispatchMessages sender, IDomainEvents domainEvents, ReturnToSenderDequeuer returnToSender, RetryingManager retryingManager)
        {
            this.store = store;
            this.sender = sender;
            this.returnToSender = returnToSender;
            this.retryingManager = retryingManager;
            this.domainEvents = domainEvents;
            corruptedReplyToHeaderStrategy = new CorruptedReplyToHeaderStrategy(RuntimeEnvironment.MachineName);
        }

        public async Task<bool> ProcessBatches(IAsyncDocumentSession session, CancellationToken cancellationToken)
        {
            return await ForwardCurrentBatch(session, cancellationToken).ConfigureAwait(false) || await MoveStagedBatchesToForwardingBatch(session).ConfigureAwait(false);
        }

        private async Task<bool> MoveStagedBatchesToForwardingBatch(IAsyncDocumentSession session)
        {
            try
            {
                if (Log.IsDebugEnabled)
                {
                    Log.Debug("Looking for batch to stage.");
                }
                isRecoveringFromPrematureShutdown = false;

                var stagingBatch = await session.Query<RetryBatch>()
                    .Customize(q => q.Include<RetryBatch, FailedMessageRetry>(b => b.FailureRetries))
                    .FirstOrDefaultAsync(b => b.Status == RetryBatchStatus.Staging)
                    .ConfigureAwait(false);

                if (stagingBatch != null)
                {
                    Log.Info($"Staging batch {stagingBatch.Id}.");
                    redirects = await MessageRedirectsCollection.GetOrCreate(session).ConfigureAwait(false);
                    var stagedMessages = await Stage(stagingBatch, session).ConfigureAwait(false);
                    var skippedMessages = stagingBatch.InitialBatchSize - stagedMessages;
                    await retryingManager.Skip(stagingBatch.RequestId, stagingBatch.RetryType, skippedMessages)
                        .ConfigureAwait(false);

                    if (stagedMessages > 0)
                    {
                        Log.Info($"Batch {stagingBatch.Id} with {stagedMessages} messages staged and {skippedMessages} skipped ready to be forwarded.");
                        await session.StoreAsync(new RetryBatchNowForwarding
                            {
                                RetryBatchId = stagingBatch.Id
                            }, RetryBatchNowForwarding.Id)
                            .ConfigureAwait(false);
                    }

                    return true;
                }

                Log.Info("No batch found to stage.");
                return false;
            }
            catch (RetryStagingException)
            {
                return true; //Execute another staging attempt immediately
            }
        }

        private async Task<bool> ForwardCurrentBatch(IAsyncDocumentSession session, CancellationToken cancellationToken)
        {
            if (Log.IsDebugEnabled)
            {
                Log.Debug("Looking for batch to forward.");
            }

            var nowForwarding = await session.Include<RetryBatchNowForwarding, RetryBatch>(r => r.RetryBatchId)
                .LoadAsync<RetryBatchNowForwarding>(RetryBatchNowForwarding.Id)
                .ConfigureAwait(false);

            if (nowForwarding != null)
            {
                if (Log.IsDebugEnabled)
                {
                    Log.Debug($"Loading batch {nowForwarding.RetryBatchId} for forwarding.");
                }

                var forwardingBatch = await session.LoadAsync<RetryBatch>(nowForwarding.RetryBatchId, cancellationToken).ConfigureAwait(false);

                if (forwardingBatch != null)
                {
                    Log.Info($"Forwarding batch {forwardingBatch.Id}.");
                    await Forward(forwardingBatch, session, cancellationToken)
                        .ConfigureAwait(false);
                    Log.DebugFormat("Retry batch {0} forwarded.", forwardingBatch.Id);
                }
                else
                {
                    Log.Warn($"Could not find retry batch {nowForwarding.RetryBatchId} to forward.");
                }

                if (Log.IsDebugEnabled)
                {
                    Log.Debug("Removing forwarding document.");
                }

                session.Delete(nowForwarding);
                return true;
            }

            Log.Info("No batch found to forward.");
            return false;
        }

        async Task Forward(RetryBatch forwardingBatch, IAsyncDocumentSession session, CancellationToken cancellationToken)
        {
            var messageCount = forwardingBatch.FailureRetries.Count;

            await retryingManager.Forwarding(forwardingBatch.RequestId, forwardingBatch.RetryType)
                .ConfigureAwait(false);

            if (isRecoveringFromPrematureShutdown)
            {
                Log.Warn($"Recovering from premature shutdown. Starting forwarder for batch {forwardingBatch.Id} in timeout mode.");
                await returnToSender.Run(forwardingBatch.Id, IsPartOfStagedBatch(forwardingBatch.StagingId), cancellationToken, null)
                    .ConfigureAwait(false);
                await retryingManager.ForwardedBatch(forwardingBatch.RequestId, forwardingBatch.RetryType, forwardingBatch.InitialBatchSize)
                    .ConfigureAwait(false);
            }
            else
            {
                if (messageCount == 0)
                {
                    Log.Info($"Skipping forwarding of batch {forwardingBatch.Id}: no messages to forward.");
                }
                else
                {
                    Log.Info($"Starting forwarder for batch {forwardingBatch.Id} with {messageCount} messages in counting mode.");
                    await returnToSender.Run(forwardingBatch.Id, IsPartOfStagedBatch(forwardingBatch.StagingId), cancellationToken, messageCount)
                        .ConfigureAwait(false);
                }
                await retryingManager.ForwardedBatch(forwardingBatch.RequestId, forwardingBatch.RetryType, messageCount)
                    .ConfigureAwait(false);
            }

            session.Delete(forwardingBatch);

            Log.Info($"Done forwarding batch {forwardingBatch.Id}.");
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
                .Distinct(FailedMessageEqualityComparer.Instance)
                .ToArray();

            foreach (var failedMessageRetry in failedMessageRetryDocs)
            {
                if (failedMessageRetry != null)
                {
                    session.Advanced.Evict(failedMessageRetry);
                }
            }

            var failedMessagesById = matchingFailures.ToDictionary(x => x.FailedMessageId, x => x);

            if (!failedMessagesById.Any())
            {
                Log.Info($"Retry batch {stagingBatch.Id} cancelled as all matching unresolved messages are already marked for retry as part of another batch.");
                session.Delete(stagingBatch);
                return 0;
            }

            var failedMessagesDocs = await session.LoadAsync<FailedMessage>(failedMessagesById.Keys).ConfigureAwait(false);
            var messages = failedMessagesDocs
                .Where(m => m != null)
                .ToArray();

            Log.Info($"Staging {messages.Length} messages for retry batch {stagingBatch.Id} with staging attempt Id {stagingId}.");

            await Task.WhenAll(messages.Select(m => TryStageMessage(m, stagingId, failedMessagesById[m.Id])).ToArray()).ConfigureAwait(false);

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
            Log.Info($"Retry batch {stagingBatch.Id} staged with Staging Id {stagingBatch.StagingId} and {stagingBatch.FailureRetries.Count} matching failure retries");
            return messages.Length;
        }

        async Task TryStageMessage(FailedMessage message, string stagingId, FailedMessageRetry failedMessageRetry)
        {
            try
            {
                await StageMessage(message, stagingId).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                var incrementedAttempts = failedMessageRetry.StageAttempts + 1;

                if (incrementedAttempts < MaxStagingAttempts)
                {
                    Log.Warn($"Attempt {incrementedAttempts} of {MaxStagingAttempts} to stage a retry message {message.UniqueMessageId} failed", e);

                    await IncrementAttemptCounter(failedMessageRetry)
                        .ConfigureAwait(false);

                }
                else
                {
                    Log.Error($"Retry message {message.UniqueMessageId} reached its staging retry limit ({MaxStagingAttempts}) and is going to be removed from the batch.", e);

                    await store.AsyncDatabaseCommands.DeleteAsync(FailedMessageRetry.MakeDocumentId(message.UniqueMessageId), null)
                        .ConfigureAwait(false);

                    await domainEvents.Raise(new MessageFailedInStaging
                    {
                        UniqueMessageId = message.UniqueMessageId
                    }).ConfigureAwait(false);
                }
                throw new RetryStagingException();
            }
        }

        async Task IncrementAttemptCounter(FailedMessageRetry message)
        {
            try
            {
                await store.AsyncDatabaseCommands.PatchAsync(message.Id,
                    new[]
                    {
                        new PatchRequest
                        {
                            Type = PatchCommandType.Set,
                            Name = "StageAttempts",
                            Value = message.StageAttempts + 1
                        }
                    }).ConfigureAwait(false);
            }
            catch (ConcurrencyException)
            {
                Log.DebugFormat("Ignoring concurrency exception while incrementing staging attempt count for {0}", message.FailedMessageId);
            }
        }

        

        Task StageMessage(FailedMessage message, string stagingId)
        {
            message.Status = FailedMessageStatus.RetryIssued;

            var attempt = message.ProcessingAttempts.Last();

            var headersToRetryWith = HeaderFilter.RemoveErrorMessageHeaders(attempt.Headers);

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
        IDocumentStore store;
        IDispatchMessages sender;
        IDomainEvents domainEvents;
        ReturnToSenderDequeuer returnToSender;
        RetryingManager retryingManager;
        MessageRedirectsCollection redirects;
        bool isRecoveringFromPrematureShutdown = true;
        CorruptedReplyToHeaderStrategy corruptedReplyToHeaderStrategy;

        static ILog Log = LogManager.GetLogger(typeof(RetryProcessor));
    }
}