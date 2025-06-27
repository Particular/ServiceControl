namespace ServiceControl.Recoverability
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Infrastructure.DomainEvents;
    using MessageFailures;
    using Microsoft.Extensions.Logging;
    using NServiceBus.Routing;
    using NServiceBus.Support;
    using NServiceBus.Transport;
    using Persistence.MessageRedirects;
    using ServiceControl.Persistence;

    class RetryProcessor
    {
        public RetryProcessor(
            IRetryBatchesDataStore store,
            IDomainEvents domainEvents,
            ReturnToSenderDequeuer returnToSender,
            RetryingManager retryingManager,
            Lazy<IMessageDispatcher> messageDispatcher,
            ILogger<RetryProcessor> logger)
        {
            this.store = store;
            this.returnToSender = returnToSender;
            this.retryingManager = retryingManager;
            this.domainEvents = domainEvents;
            this.messageDispatcher = messageDispatcher;
            this.logger = logger;
            corruptedReplyToHeaderStrategy = new CorruptedReplyToHeaderStrategy(RuntimeEnvironment.MachineName, logger);
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
                logger.LogDebug("Looking for batch to stage");

                isRecoveringFromPrematureShutdown = false;

                var stagingBatch = await manager.GetStagingBatch();

                if (stagingBatch != null)
                {
                    logger.LogInformation("Staging batch {StagingBatchId}", stagingBatch.Id);
                    redirects = await manager.GetOrCreateMessageRedirectsCollection();
                    var stagedMessages = await Stage(stagingBatch, manager);
                    var skippedMessages = stagingBatch.InitialBatchSize - stagedMessages;
                    await retryingManager.Skip(stagingBatch.RequestId, stagingBatch.RetryType, skippedMessages);

                    if (stagedMessages > 0)
                    {
                        logger.LogInformation("Batch {StagingBatchId} with {StagedMessages} messages staged and {SkippedMessages} skipped ready to be forwarded", stagingBatch.Id, stagedMessages, skippedMessages);
                        await manager.Store(new RetryBatchNowForwarding
                        {
                            RetryBatchId = stagingBatch.Id
                        });
                    }

                    return true;
                }

                logger.LogDebug("No batch found to stage");
                return false;
            }
            catch (RetryStagingException)
            {
                return true; //Execute another staging attempt immediately
            }
        }

        async Task<bool> ForwardCurrentBatch(IRetryBatchesManager manager, CancellationToken cancellationToken)
        {
            logger.LogDebug("Looking for batch to forward");

            var nowForwarding = await manager.GetRetryBatchNowForwarding();

            if (nowForwarding != null)
            {
                logger.LogDebug("Loading batch {RetryBatchId} for forwarding", nowForwarding.RetryBatchId);

                var forwardingBatch = await manager.GetRetryBatch(nowForwarding.RetryBatchId, cancellationToken);

                if (forwardingBatch != null)
                {
                    logger.LogInformation("Forwarding batch {RetryBatchId}", forwardingBatch.Id);

                    await Forward(forwardingBatch, manager, cancellationToken);

                    logger.LogDebug("Retry batch {RetryBatchId} forwarded", forwardingBatch.Id);
                }
                else
                {
                    logger.LogWarning("Could not find retry batch {RetryBatchId} to forward", nowForwarding.RetryBatchId);
                }

                logger.LogDebug("Removing forwarding document");

                manager.Delete(nowForwarding);
                return true;
            }

            logger.LogDebug("No batch found to forward");
            return false;
        }

        async Task Forward(RetryBatch forwardingBatch, IRetryBatchesManager manager, CancellationToken cancellationToken)
        {
            var messageCount = forwardingBatch.FailureRetries.Count;

            await retryingManager.Forwarding(forwardingBatch.RequestId, forwardingBatch.RetryType);

            if (isRecoveringFromPrematureShutdown)
            {
                logger.LogWarning("Recovering from premature shutdown. Starting forwarder for batch {ForwardingBatchId} in timeout mode", forwardingBatch.Id);
                await returnToSender.Run(forwardingBatch.Id, IsPartOfStagedBatch(forwardingBatch.StagingId), null, cancellationToken);
                await retryingManager.ForwardedBatch(forwardingBatch.RequestId, forwardingBatch.RetryType, forwardingBatch.InitialBatchSize);
            }
            else
            {
                if (messageCount == 0)
                {
                    logger.LogInformation("Skipping forwarding of batch {ForwardingBatchId}: no messages to forward", forwardingBatch.Id);
                }
                else
                {
                    logger.LogInformation("Starting forwarder for batch {ForwardingBatchId} with {BatchMessageCount} messages in counting mode", forwardingBatch.Id, messageCount);
                    await returnToSender.Run(forwardingBatch.Id, IsPartOfStagedBatch(forwardingBatch.StagingId), messageCount, cancellationToken);
                }

                await retryingManager.ForwardedBatch(forwardingBatch.RequestId, forwardingBatch.RetryType, messageCount);
            }

            manager.Delete(forwardingBatch);

            logger.LogInformation("Done forwarding batch {ForwardingBatchId}", forwardingBatch.Id);
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
                logger.LogInformation("Retry batch {RetryBatchId} cancelled as all matching unresolved messages are already marked for retry as part of another batch", stagingBatch.Id);
                manager.Delete(stagingBatch);
                return 0;
            }

            var failedMessagesDocs = await manager.GetFailedMessages(failedMessageRetriesById.Keys);
            var messages = failedMessagesDocs.Where(m => m != null).ToArray();

            logger.LogInformation("Staging {MessageCount} messages for retry batch {RetryBatchId} with staging attempt Id {StagingId}", messages.Length, stagingBatch.Id, stagingId);

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
            logger.LogInformation("Retry batch {RetryBatchId} staged with Staging Id {StagingId} and {RetryFailureCount} matching failure retries", stagingBatch.Id, stagingBatch.StagingId, stagingBatch.FailureRetries.Count);
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
                    logger.LogWarning(e, "Attempt {StagingRetryAttempt} of {StagingRetryLimit} to stage a retry message {RetryMessageId} failed", incrementedAttempts, MaxStagingAttempts, uniqueMessageId);

                    await store.IncrementAttemptCounter(failedMessageRetry);
                }
                else
                {
                    logger.LogError(e, "Retry message {RetryMessageId} reached its staging retry limit ({StagingRetryLimit}) and is going to be removed from the batch", uniqueMessageId, MaxStagingAttempts);

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

        readonly ILogger<RetryProcessor> logger;
    }
}