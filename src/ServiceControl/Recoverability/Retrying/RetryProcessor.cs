namespace ServiceControl.Recoverability
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Infrastructure.Auth;
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
            IRetryStagingStore store,
            IMessageRedirectsDataStore redirectsStore,
            IDomainEvents domainEvents,
            ReturnToSenderDequeuer returnToSender,
            RetryingManager retryingManager,
            Lazy<IMessageDispatcher> messageDispatcher,
            IMessageActionAuditLog auditLog,
            ILogger<RetryProcessor> logger)
        {
            this.store = store;
            this.redirectsStore = redirectsStore;
            this.returnToSender = returnToSender;
            this.retryingManager = retryingManager;
            this.domainEvents = domainEvents;
            this.messageDispatcher = messageDispatcher;
            this.auditLog = auditLog;
            this.logger = logger;
            corruptedReplyToHeaderStrategy = new CorruptedReplyToHeaderStrategy(RuntimeEnvironment.MachineName, logger);
        }

        Task Enqueue(TransportOperations outgoingMessages, CancellationToken cancellationToken)
        {
            return messageDispatcher.Value.Dispatch(outgoingMessages, new TransportTransaction(), cancellationToken);
        }

        public async Task<bool> ProcessBatches(CancellationToken cancellationToken = default) =>
            await ForwardCurrentBatch(cancellationToken) || await MoveStagedBatchesToForwardingBatch(cancellationToken);

        async Task<bool> MoveStagedBatchesToForwardingBatch(CancellationToken cancellationToken)
        {
            try
            {
                logger.LogDebug("Looking for batch to stage");

                isRecoveringFromPrematureShutdown = false;

                var stagingBatch = await store.GetStagingBatch();

                if (stagingBatch != null)
                {
                    logger.LogInformation("Staging batch {StagingBatchId}", stagingBatch.Id);
                    redirects = await redirectsStore.GetRedirects();
                    var stagedMessages = await Stage(stagingBatch, cancellationToken);
                    var skippedMessages = stagingBatch.InitialBatchSize - stagedMessages;
                    await retryingManager.Skip(stagingBatch.RequestId, stagingBatch.RetryType, skippedMessages, cancellationToken);

                    if (stagedMessages > 0)
                    {
                        logger.LogInformation("Batch {StagingBatchId} with {StagedMessages} messages staged and {SkippedMessages} skipped ready to be forwarded", stagingBatch.Id, stagedMessages, skippedMessages);
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

        async Task<bool> ForwardCurrentBatch(CancellationToken cancellationToken)
        {
            logger.LogDebug("Looking for batch to forward");

            var forwardingBatchId = await store.GetForwardingBatchId();

            if (forwardingBatchId == null)
            {
                logger.LogDebug("No batch found to forward");
                return false;
            }

            logger.LogDebug("Loading batch {RetryBatchId} for forwarding", forwardingBatchId);

            var forwardingBatch = await store.GetBatch(forwardingBatchId, cancellationToken);

            if (forwardingBatch != null)
            {
                logger.LogInformation("Forwarding batch {RetryBatchId}", forwardingBatch.Id);

                await Forward(forwardingBatch, cancellationToken);

                logger.LogDebug("Retry batch {RetryBatchId} forwarded", forwardingBatch.Id);
            }
            else
            {
                logger.LogWarning("Could not find retry batch {RetryBatchId} to forward", forwardingBatchId);
            }

            logger.LogDebug("Removing forwarding pointer");

            await store.CompleteForwarding(forwardingBatchId);
            return true;
        }

        async Task Forward(RetryBatch forwardingBatch, CancellationToken cancellationToken)
        {
            var messageCount = forwardingBatch.MessageCount;

            await retryingManager.Forwarding(forwardingBatch.RequestId, forwardingBatch.RetryType, cancellationToken);

            if (isRecoveringFromPrematureShutdown)
            {
                logger.LogWarning("Recovering from premature shutdown. Starting forwarder for batch {ForwardingBatchId} in timeout mode", forwardingBatch.Id);
                await returnToSender.Run(forwardingBatch.Id, IsPartOfStagedBatch(forwardingBatch.StagingId), null, cancellationToken);
                await retryingManager.ForwardedBatch(forwardingBatch.RequestId, forwardingBatch.RetryType, forwardingBatch.InitialBatchSize, cancellationToken);
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

                await retryingManager.ForwardedBatch(forwardingBatch.RequestId, forwardingBatch.RetryType, messageCount, cancellationToken);
            }

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

        async Task<int> Stage(RetryBatch stagingBatch, CancellationToken cancellationToken)
        {
            var stagingId = Guid.NewGuid().ToString();

            var messagesToStage = await store.GetMessagesToStage(stagingBatch.Id);

            if (messagesToStage.Length == 0)
            {
                logger.LogInformation("Retry batch {RetryBatchId} cancelled as it has no messages left to stage", stagingBatch.Id);
                await store.DiscardBatch(stagingBatch.Id);
                return 0;
            }

            var stageAttemptsById = messagesToStage.ToDictionary(messageToStage => messageToStage.UniqueMessageId, messageToStage => messageToStage.StageAttempts);

            logger.LogInformation("Staging {MessageCount} messages for retry batch {RetryBatchId} with staging attempt Id {StagingId}", messagesToStage.Length, stagingBatch.Id, stagingId);

            var previousAttemptFailed = messagesToStage.Any(messageToStage => messageToStage.StageAttempts > 0);
            var transportOperations = new TransportOperation[messagesToStage.Length];
            var current = 0;
            foreach (var messageToStage in messagesToStage)
            {
                transportOperations[current++] = ToTransportOperation(messageToStage, stagingId);
            }

            await TryDispatch(stagingBatch.Id, transportOperations, messagesToStage, stageAttemptsById, previousAttemptFailed, cancellationToken);

            AuditStagedMessages(stagingBatch, messagesToStage);

            if (stagingBatch.RetryType != RetryType.FailureGroup) //FailureGroup published on completion of entire group
            {
                var failedIds = messagesToStage.Select(x => x.UniqueMessageId).ToArray();
                await domainEvents.Raise(new MessagesSubmittedForRetry
                {
                    FailedMessageIds = failedIds,
                    NumberOfFailedMessages = failedIds.Length,
                    Context = stagingBatch.Context
                }, cancellationToken);
            }

            await store.MarkBatchAsForwarding(stagingBatch.Id, stagingId, [.. stageAttemptsById.Keys]);

            logger.LogInformation("Retry batch {RetryBatchId} staged with Staging Id {StagingId} and {RetryFailureCount} matching failure retries", stagingBatch.Id, stagingId, messagesToStage.Length);
            return messagesToStage.Length;
        }

        // Emits one per-message audit entry for each message actually staged for retry, for every retry
        // type: the API emits the operation-level entry, this emits the per-message entries, correlated by
        // OperationId. Skipped for batches without an OperationId (legacy in-flight commands sent without
        // the audit headers).
        void AuditStagedMessages(RetryBatch stagingBatch, IReadOnlyCollection<StagingMessage> messages)
        {
            if (string.IsNullOrEmpty(stagingBatch.OperationId))
            {
                return;
            }

            var user = new AuditUser(stagingBatch.InitiatedById, stagingBatch.InitiatedByName);
            var scope = stagingBatch.RetryType switch
            {
                RetryType.All => MessageActionScope.All,
                RetryType.AllForEndpoint => MessageActionScope.Endpoint,
                RetryType.ByQueueAddress => MessageActionScope.Queue,
                RetryType.FailureGroup => MessageActionScope.Group,
                RetryType.MultipleMessages => MessageActionScope.Batch,
                RetryType.SingleMessage => MessageActionScope.Single,
                RetryType.Unknown => MessageActionScope.Single,
                _ => MessageActionScope.Single
            };
            var permission = stagingBatch.RetryType == RetryType.FailureGroup
                ? Permissions.ErrorRecoverabilityGroupsRetry
                : Permissions.ErrorMessagesRetry;

            foreach (var message in messages)
            {
                auditLog.MessageAction(user, MessageActionKind.Retry, permission, scope, message.UniqueMessageId, stagingBatch.OperationId);
            }
        }

        Task TryDispatch(string batchId, TransportOperation[] transportOperations, IReadOnlyCollection<StagingMessage> messages,
            IReadOnlyDictionary<string, int> stageAttemptsById, bool previousAttemptFailed, CancellationToken cancellationToken)
        {
            return previousAttemptFailed ? ConcurrentDispatchToTransport(transportOperations, stageAttemptsById, cancellationToken) :
                BatchDispatchToTransport(batchId, transportOperations, messages, cancellationToken);
        }

        Task ConcurrentDispatchToTransport(IReadOnlyCollection<TransportOperation> transportOperations, IReadOnlyDictionary<string, int> stageAttemptsById, CancellationToken cancellationToken)
        {
            var tasks = new List<Task>(transportOperations.Count);
            foreach (var transportOperation in transportOperations)
            {
                tasks.Add(TryStageMessage(transportOperation, stageAttemptsById, cancellationToken));
            }
            return Task.WhenAll(tasks);
        }

        async Task BatchDispatchToTransport(string batchId, TransportOperation[] transportOperations, IReadOnlyCollection<StagingMessage> messages, CancellationToken cancellationToken)
        {
            try
            {
                await Enqueue(new TransportOperations(transportOperations), cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception e)
            {
                logger.LogWarning(e, "Attempt 1 of {MaxStagingAttempts} to stage the {MessageCount} messages of retry batch {RetryBatchId} failed", MaxStagingAttempts, messages.Count, batchId);

                await store.RecordStagingFailure([.. messages.Select(message => message.UniqueMessageId)]);

                throw new RetryStagingException(e);
            }
        }

        async Task TryStageMessage(TransportOperation transportOperation, IReadOnlyDictionary<string, int> stageAttemptsById, CancellationToken cancellationToken)
        {
            var uniqueMessageId = transportOperation.Message.Headers["ServiceControl.Retry.UniqueMessageId"];

            try
            {
                await Enqueue(new TransportOperations(transportOperation), cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception e)
            {
                var incrementedAttempts = stageAttemptsById[uniqueMessageId] + 1;

                if (incrementedAttempts < MaxStagingAttempts)
                {
                    logger.LogWarning(e, "Attempt {StagingRetryAttempt} of {StagingRetryLimit} to stage a retry message {RetryMessageId} failed", incrementedAttempts, MaxStagingAttempts, uniqueMessageId);

                    await store.IncrementStagingAttempts(uniqueMessageId);
                }
                else
                {
                    logger.LogError(e, "Retry message {RetryMessageId} reached its staging retry limit ({StagingRetryLimit}) and is going to be removed from the batch", uniqueMessageId, MaxStagingAttempts);

                    await store.RemoveFromBatch(uniqueMessageId);

                    await domainEvents.Raise(new MessageFailedInStaging
                    {
                        UniqueMessageId = uniqueMessageId
                    }, cancellationToken);
                }

                throw new RetryStagingException(e);
            }
        }

        TransportOperation ToTransportOperation(StagingMessage message, string stagingId)
        {
            var headersToRetryWith = HeaderFilter.RemoveErrorMessageHeaders(message.Headers);

            var addressOfFailingEndpoint = message.FailingEndpointAddress;

            var redirect = redirects.FindByAddress(addressOfFailingEndpoint);

            if (redirect != null)
            {
                addressOfFailingEndpoint = redirect.ToPhysicalAddress;
            }

            headersToRetryWith["ServiceControl.TargetEndpointAddress"] = addressOfFailingEndpoint;
            headersToRetryWith["ServiceControl.Retry.UniqueMessageId"] = message.UniqueMessageId;
            headersToRetryWith["ServiceControl.Retry.StagingId"] = stagingId;
            headersToRetryWith["ServiceControl.Retry.Attempt.MessageId"] = message.AttemptMessageId;

            corruptedReplyToHeaderStrategy.FixCorruptedReplyToHeader(headersToRetryWith);

            var transportMessage = new OutgoingMessage(message.Id, headersToRetryWith, Array.Empty<byte>());
            return new TransportOperation(transportMessage, new UnicastAddressTag(returnToSender.InputAddress));
        }

        readonly IDomainEvents domainEvents;
        readonly IRetryStagingStore store;
        readonly IMessageRedirectsDataStore redirectsStore;
        readonly ReturnToSenderDequeuer returnToSender;
        readonly RetryingManager retryingManager;
        readonly Lazy<IMessageDispatcher> messageDispatcher;
        readonly IMessageActionAuditLog auditLog;
        IReadOnlyList<MessageRedirect> redirects;
        bool isRecoveringFromPrematureShutdown = true;
        CorruptedReplyToHeaderStrategy corruptedReplyToHeaderStrategy;
        protected internal const int MaxStagingAttempts = 5;

        readonly ILogger<RetryProcessor> logger;
    }
}
