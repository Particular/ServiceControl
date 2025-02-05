namespace ServiceControl.Recoverability
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Infrastructure.DomainEvents;
    using MessageFailures;
    using NServiceBus.Logging;
    using NServiceBus.Routing;
    using NServiceBus.Support;
    using NServiceBus.Transport;
    using Persistence.MessageRedirects;
    using ServiceControl.Persistence;

    class RetryProcessor
    {
        public RetryProcessor(IRetryBatchesDataStore store, IDomainEvents domainEvents, ReturnToSenderDequeuer returnToSender, RetryingManager retryingManager, Lazy<IMessageDispatcher> messageDispatcher)
        {
            this.store = store;
            this.returnToSender = returnToSender;
            this.retryingManager = retryingManager;
            this.domainEvents = domainEvents;
            this.messageDispatcher = messageDispatcher;
            corruptedReplyToHeaderStrategy = new CorruptedReplyToHeaderStrategy(RuntimeEnvironment.MachineName);
        }

        Task Enqueue(TransportOperations outgoingMessages)
        {
            return messageDispatcher.Value.Dispatch(outgoingMessages, new TransportTransaction());
        }

        public async Task<bool> ProcessBatches(CancellationToken cancellationToken = default)
        {
            using (var manager = await store.CreateRetryBatchesManager())
            {
                var result = await ForwardCurrentBatch(manager, cancellationToken) || await MoveStagedBatchesToForwardingBatch(manager);

                await manager.SaveChanges();

                return result;
            }
        }

        async Task<bool> MoveStagedBatchesToForwardingBatch(IRetryBatchesManager manager)
        {
            try
            {
                if (Logger.IsDebugEnabled)
                {
                    Logger.Debug("Looking for batch to stage.");
                }

                isRecoveringFromPrematureShutdown = false;

                var stagingBatch = await manager.GetStagingBatch();

                if (stagingBatch != null)
                {
                    Logger.Info($"Staging batch {stagingBatch.Id}.");
                    redirects = await manager.GetOrCreateMessageRedirectsCollection();
                    var stagedMessages = await Stage(stagingBatch, manager);
                    var skippedMessages = stagingBatch.InitialBatchSize - stagedMessages;
                    await retryingManager.Skip(stagingBatch.RequestId, stagingBatch.RetryType, skippedMessages);

                    if (stagedMessages > 0)
                    {
                        Logger.Info($"Batch {stagingBatch.Id} with {stagedMessages} messages staged and {skippedMessages} skipped ready to be forwarded.");
                        await manager.Store(new RetryBatchNowForwarding
                        {
                            RetryBatchId = stagingBatch.Id
                        });
                    }

                    return true;
                }

                Logger.Debug("No batch found to stage.");
                return false;
            }
            catch (RetryStagingException)
            {
                return true; //Execute another staging attempt immediately
            }
        }

        async Task<bool> ForwardCurrentBatch(IRetryBatchesManager manager, CancellationToken cancellationToken)
        {
            if (Logger.IsDebugEnabled)
            {
                Logger.Debug("Looking for batch to forward.");
            }

            var nowForwarding = await manager.GetRetryBatchNowForwarding();

            if (nowForwarding != null)
            {
                if (Logger.IsDebugEnabled)
                {
                    Logger.Debug($"Loading batch {nowForwarding.RetryBatchId} for forwarding.");
                }

                var forwardingBatch = await manager.GetRetryBatch(nowForwarding.RetryBatchId, cancellationToken);

                if (forwardingBatch != null)
                {
                    if (Logger.IsDebugEnabled)
                    {
                        Logger.Info($"Forwarding batch {forwardingBatch.Id}.");
                    }

                    await Forward(forwardingBatch, manager, cancellationToken);

                    if (Logger.IsDebugEnabled)
                    {
                        Logger.DebugFormat("Retry batch {0} forwarded.", forwardingBatch.Id);
                    }
                }
                else
                {
                    Logger.Warn($"Could not find retry batch {nowForwarding.RetryBatchId} to forward.");
                }

                if (Logger.IsDebugEnabled)
                {
                    Logger.Debug("Removing forwarding document.");
                }

                manager.Delete(nowForwarding);
                return true;
            }

            Logger.Debug("No batch found to forward.");
            return false;
        }

        async Task Forward(RetryBatch forwardingBatch, IRetryBatchesManager manager, CancellationToken cancellationToken)
        {
            var messageCount = forwardingBatch.FailureRetries.Count;

            await retryingManager.Forwarding(forwardingBatch.RequestId, forwardingBatch.RetryType);

            if (isRecoveringFromPrematureShutdown)
            {
                Logger.Warn($"Recovering from premature shutdown. Starting forwarder for batch {forwardingBatch.Id} in timeout mode.");
                await returnToSender.Run(forwardingBatch.Id, IsPartOfStagedBatch(forwardingBatch.StagingId), null, cancellationToken);
                await retryingManager.ForwardedBatch(forwardingBatch.RequestId, forwardingBatch.RetryType, forwardingBatch.InitialBatchSize);
            }
            else
            {
                if (messageCount == 0)
                {
                    Logger.Info($"Skipping forwarding of batch {forwardingBatch.Id}: no messages to forward.");
                }
                else
                {
                    Logger.Info($"Starting forwarder for batch {forwardingBatch.Id} with {messageCount} messages in counting mode.");
                    await returnToSender.Run(forwardingBatch.Id, IsPartOfStagedBatch(forwardingBatch.StagingId), messageCount, cancellationToken);
                }

                await retryingManager.ForwardedBatch(forwardingBatch.RequestId, forwardingBatch.RetryType, messageCount);
            }

            manager.Delete(forwardingBatch);

            Logger.Info($"Done forwarding batch {forwardingBatch.Id}.");
        }

        static Predicate<MessageContext> IsPartOfStagedBatch(string stagingId)
        {
            return m =>
            {
                var messageStagingId = m.Headers["ServiceControl.Retry.StagingId"];
                return messageStagingId == stagingId;
            };
        }

        async Task<int> Stage(RetryBatch stagingBatch, IRetryBatchesManager manager)
        {
            var stagingId = Guid.NewGuid().ToString();

            var failedMessageRetryDocs = await manager.GetFailedMessageRetries(stagingBatch.FailureRetries);

            var failedMessageRetriesById = failedMessageRetryDocs
                .Where(r => r != null && r.RetryBatchId == stagingBatch.Id)
                .Distinct(FailedMessageEqualityComparer.Instance)
                .ToDictionary(x => x.FailedMessageId, x => x);

            foreach (var failedMessageRetry in failedMessageRetryDocs)
            {
                if (failedMessageRetry != null)
                {
                    manager.Evict(failedMessageRetry);
                }
            }

            if (failedMessageRetriesById.Count == 0)
            {
                Logger.Info($"Retry batch {stagingBatch.Id} cancelled as all matching unresolved messages are already marked for retry as part of another batch.");
                manager.Delete(stagingBatch);
                return 0;
            }

            var failedMessagesDocs = await manager.GetFailedMessages(failedMessageRetriesById.Keys);
            //filtering on Unresolved, since we don't want messages to be able to be retried while the previous batch was still pending
            var messages = failedMessagesDocs.Where(m => m != null && m.Status == FailedMessageStatus.Unresolved).ToArray();

            Logger.Info($"Staging {messages.Length} messages for retry batch {stagingBatch.Id} with staging attempt Id {stagingId}.");

            var previousAttemptFailed = false;
            var transportOperations = new TransportOperation[messages.Length];
            var current = 0;
            foreach (var failedMessage in messages)
            {
                transportOperations[current++] = ToTransportOperation(failedMessage, stagingId);

                if (!previousAttemptFailed)
                {
                    previousAttemptFailed = failedMessageRetriesById[failedMessage.Id].StageAttempts > 0;
                }

                // should not be done concurrently due to sessions not being thread safe
                failedMessage.Status = FailedMessageStatus.RetryIssued;
                await manager.CancelExpiration(failedMessage);
            }

            await TryDispatch(transportOperations, messages, failedMessageRetriesById, stagingId, previousAttemptFailed);

            if (stagingBatch.RetryType != RetryType.FailureGroup) //FailureGroup published on completion of entire group
            {
                var failedIds = messages.Select(x => x.UniqueMessageId).ToArray();
                await domainEvents.Raise(new MessagesSubmittedForRetry
                {
                    FailedMessageIds = failedIds,
                    NumberOfFailedMessages = failedIds.Length,
                    Context = stagingBatch.Context
                });
            }

            var msgLookup = messages.ToLookup(x => x.Id);

            stagingBatch.Status = RetryBatchStatus.Forwarding;
            stagingBatch.StagingId = stagingId;
            stagingBatch.FailureRetries = failedMessageRetriesById.Values.Where(x => msgLookup[x.FailedMessageId].Any()).Select(x => x.Id).ToArray();
            Logger.Info($"Retry batch {stagingBatch.Id} staged with Staging Id {stagingBatch.StagingId} and {stagingBatch.FailureRetries.Count} matching failure retries");
            return messages.Length;
        }

        Task TryDispatch(TransportOperation[] transportOperations, IReadOnlyCollection<FailedMessage> messages,
            IReadOnlyDictionary<string, FailedMessageRetry> failedMessageRetriesById, string stagingId,
            bool previousAttemptFailed)
        {
            return previousAttemptFailed ? ConcurrentDispatchToTransport(transportOperations, failedMessageRetriesById) :
                BatchDispatchToTransport(transportOperations, messages, failedMessageRetriesById, stagingId);
        }

        Task ConcurrentDispatchToTransport(IReadOnlyCollection<TransportOperation> transportOperations, IReadOnlyDictionary<string, FailedMessageRetry> failedMessageRetriesById)
        {
            var tasks = new List<Task>(transportOperations.Count);
            foreach (var transportOperation in transportOperations)
            {
                tasks.Add(TryStageMessage(transportOperation, failedMessageRetriesById[transportOperation.Message.MessageId]));
            }
            return Task.WhenAll(tasks);
        }

        async Task BatchDispatchToTransport(TransportOperation[] transportOperations, IReadOnlyCollection<FailedMessage> messages,
            IReadOnlyDictionary<string, FailedMessageRetry> failedMessageRetriesById, string stagingId)
        {
            try
            {
                await Enqueue(new TransportOperations(transportOperations));
            }
            catch (Exception e)
            {
                await store.RecordFailedStagingAttempt(messages, failedMessageRetriesById, e, MaxStagingAttempts, stagingId);

                throw new RetryStagingException(e);
            }
        }

        async Task TryStageMessage(TransportOperation transportOperation, FailedMessageRetry failedMessageRetry)
        {
            try
            {
                await Enqueue(new TransportOperations(transportOperation));
            }
            catch (Exception e)
            {
                var incrementedAttempts = failedMessageRetry.StageAttempts + 1;
                var uniqueMessageId = transportOperation.Message.Headers["ServiceControl.Retry.UniqueMessageId"];

                if (incrementedAttempts < MaxStagingAttempts)
                {
                    Logger.Warn($"Attempt {incrementedAttempts} of {MaxStagingAttempts} to stage a retry message {uniqueMessageId} failed", e);

                    await store.IncrementAttemptCounter(failedMessageRetry);
                }
                else
                {
                    Logger.Error($"Retry message {uniqueMessageId} reached its staging retry limit ({MaxStagingAttempts}) and is going to be removed from the batch.", e);

                    await store.DeleteFailedMessageRetry(uniqueMessageId);

                    await domainEvents.Raise(new MessageFailedInStaging
                    {
                        UniqueMessageId = uniqueMessageId
                    });
                }

                throw new RetryStagingException(e);
            }
        }

        TransportOperation ToTransportOperation(FailedMessage message, string stagingId)
        {
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

            var transportMessage = new OutgoingMessage(message.Id, headersToRetryWith, Array.Empty<byte>());
            return new TransportOperation(transportMessage, new UnicastAddressTag(returnToSender.InputAddress));
        }

        readonly IDomainEvents domainEvents;
        readonly IRetryBatchesDataStore store;
        readonly ReturnToSenderDequeuer returnToSender;
        readonly RetryingManager retryingManager;
        readonly Lazy<IMessageDispatcher> messageDispatcher;

        MessageRedirectsCollection redirects;
        bool isRecoveringFromPrematureShutdown = true;
        CorruptedReplyToHeaderStrategy corruptedReplyToHeaderStrategy;
        protected internal const int MaxStagingAttempts = 5;

        static readonly ILog Logger = LogManager.GetLogger(typeof(RetryProcessor));
    }
}