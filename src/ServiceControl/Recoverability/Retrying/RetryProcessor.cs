namespace ServiceControl.Recoverability
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Infrastructure.DomainEvents;
    using MessageFailures;
    using NServiceBus.Extensibility;
    using NServiceBus.Logging;
    using NServiceBus.Routing;
    using NServiceBus.Support;
    using NServiceBus.Transport;
    using Persistence.MessageRedirects;
    using ServiceControl.Persistence;

    class RetryProcessor
    {
        public RetryProcessor(IRetryBatchesDataStore store, IDomainEvents domainEvents, ReturnToSenderDequeuer returnToSender, RetryingManager retryingManager)
        {
            this.store = store;
            this.returnToSender = returnToSender;
            this.retryingManager = retryingManager;
            this.domainEvents = domainEvents;
            corruptedReplyToHeaderStrategy = new CorruptedReplyToHeaderStrategy(RuntimeEnvironment.MachineName);
        }

        Task Enqueue(IDispatchMessages sender, TransportOperations outgoingMessages)
        {
            return sender.Dispatch(outgoingMessages, new TransportTransaction(), new ContextBag());
        }

        public async Task<bool> ProcessBatches(IDispatchMessages sender, CancellationToken cancellationToken = default)
        {
            using (var manager = await store.CreateRetryBatchesManager())
            {
                var result = await ForwardCurrentBatch(manager, cancellationToken) || await MoveStagedBatchesToForwardingBatch(manager, sender);

                await manager.SaveChanges();

                return result;
            }
        }

        async Task<bool> MoveStagedBatchesToForwardingBatch(IRetryBatchesManager manager, IDispatchMessages sender)
        {
            try
            {
                if (Log.IsDebugEnabled)
                {
                    Log.Debug("Looking for batch to stage.");
                }

                isRecoveringFromPrematureShutdown = false;

                var stagingBatch = await manager.GetStagingBatch();

                if (stagingBatch != null)
                {
                    Log.Info($"Staging batch {stagingBatch.Id}.");
                    redirects = await manager.GetOrCreateMessageRedirectsCollection();
                    var stagedMessages = await Stage(stagingBatch, manager, sender);
                    var skippedMessages = stagingBatch.InitialBatchSize - stagedMessages;
                    await retryingManager.Skip(stagingBatch.RequestId, stagingBatch.RetryType, skippedMessages);

                    if (stagedMessages > 0)
                    {
                        Log.Info($"Batch {stagingBatch.Id} with {stagedMessages} messages staged and {skippedMessages} skipped ready to be forwarded.");
                        await manager.Store(new RetryBatchNowForwarding
                        {
                            RetryBatchId = stagingBatch.Id
                        }, RetryBatchNowForwarding.Id);
                    }

                    return true;
                }

                Log.Debug("No batch found to stage.");
                return false;
            }
            catch (RetryStagingException)
            {
                return true; //Execute another staging attempt immediately
            }
        }

        async Task<bool> ForwardCurrentBatch(IRetryBatchesManager manager, CancellationToken cancellationToken)
        {
            if (Log.IsDebugEnabled)
            {
                Log.Debug("Looking for batch to forward.");
            }

            var nowForwarding = await manager.GetRetryBatchNowForwarding();

            if (nowForwarding != null)
            {
                if (Log.IsDebugEnabled)
                {
                    Log.Debug($"Loading batch {nowForwarding.RetryBatchId} for forwarding.");
                }

                var forwardingBatch = await manager.GetRetryBatch(nowForwarding.RetryBatchId, cancellationToken);

                if (forwardingBatch != null)
                {
                    if (Log.IsDebugEnabled)
                    {
                        Log.Info($"Forwarding batch {forwardingBatch.Id}.");
                    }

                    await Forward(forwardingBatch, manager, cancellationToken);

                    if (Log.IsDebugEnabled)
                    {
                        Log.DebugFormat("Retry batch {0} forwarded.", forwardingBatch.Id);
                    }
                }
                else
                {
                    Log.Warn($"Could not find retry batch {nowForwarding.RetryBatchId} to forward.");
                }

                if (Log.IsDebugEnabled)
                {
                    Log.Debug("Removing forwarding document.");
                }

                manager.Delete(nowForwarding);
                return true;
            }

            Log.Debug("No batch found to forward.");
            return false;
        }

        async Task Forward(RetryBatch forwardingBatch, IRetryBatchesManager manager, CancellationToken cancellationToken)
        {
            var messageCount = forwardingBatch.FailureRetries.Count;

            await retryingManager.Forwarding(forwardingBatch.RequestId, forwardingBatch.RetryType);

            if (isRecoveringFromPrematureShutdown)
            {
                Log.Warn($"Recovering from premature shutdown. Starting forwarder for batch {forwardingBatch.Id} in timeout mode.");
                await returnToSender.Run(forwardingBatch.Id, IsPartOfStagedBatch(forwardingBatch.StagingId), null, cancellationToken);
                await retryingManager.ForwardedBatch(forwardingBatch.RequestId, forwardingBatch.RetryType, forwardingBatch.InitialBatchSize);
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
                    await returnToSender.Run(forwardingBatch.Id, IsPartOfStagedBatch(forwardingBatch.StagingId), messageCount, cancellationToken);
                }

                await retryingManager.ForwardedBatch(forwardingBatch.RequestId, forwardingBatch.RetryType, messageCount);
            }

            manager.Delete(forwardingBatch);

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

        async Task<int> Stage(RetryBatch stagingBatch, IRetryBatchesManager manager, IDispatchMessages sender)
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
                Log.Info($"Retry batch {stagingBatch.Id} cancelled as all matching unresolved messages are already marked for retry as part of another batch.");
                manager.Delete(stagingBatch);
                return 0;
            }

            var failedMessagesDocs = await manager.GetFailedMessages(failedMessageRetriesById.Keys);
            var messages = failedMessagesDocs.Where(m => m != null).ToArray();

            Log.Info($"Staging {messages.Length} messages for retry batch {stagingBatch.Id} with staging attempt Id {stagingId}.");

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
            }

            await TryDispatch(sender, transportOperations, messages, failedMessageRetriesById, stagingId, previousAttemptFailed);

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
            Log.Info($"Retry batch {stagingBatch.Id} staged with Staging Id {stagingBatch.StagingId} and {stagingBatch.FailureRetries.Count} matching failure retries");
            return messages.Length;
        }

        Task TryDispatch(IDispatchMessages sender,
            TransportOperation[] transportOperations, IReadOnlyCollection<FailedMessage> messages,
            IReadOnlyDictionary<string, FailedMessageRetry> failedMessageRetriesById, string stagingId,
            bool previousAttemptFailed)
        {
            return previousAttemptFailed ? ConcurrentDispatchToTransport(sender, transportOperations, failedMessageRetriesById) :
                BatchDispatchToTransport(sender, transportOperations, messages, failedMessageRetriesById, stagingId);
        }

        Task ConcurrentDispatchToTransport(IDispatchMessages sender, IReadOnlyCollection<TransportOperation> transportOperations, IReadOnlyDictionary<string, FailedMessageRetry> failedMessageRetriesById)
        {
            var tasks = new List<Task>(transportOperations.Count);
            foreach (var transportOperation in transportOperations)
            {
                tasks.Add(TryStageMessage(sender, transportOperation, failedMessageRetriesById[transportOperation.Message.MessageId]));
            }
            return Task.WhenAll(tasks);
        }

        async Task BatchDispatchToTransport(IDispatchMessages sender,
            TransportOperation[] transportOperations, IReadOnlyCollection<FailedMessage> messages,
            IReadOnlyDictionary<string, FailedMessageRetry> failedMessageRetriesById, string stagingId)
        {
            try
            {
                await Enqueue(sender, new TransportOperations(transportOperations));
            }
            catch (Exception e)
            {
                await store.RecordFailedStagingAttempt(messages, failedMessageRetriesById, e, MaxStagingAttempts, stagingId);

                throw new RetryStagingException(e);
            }
        }

        async Task TryStageMessage(IDispatchMessages sender, TransportOperation transportOperation, FailedMessageRetry failedMessageRetry)
        {
            try
            {
                await Enqueue(sender, new TransportOperations(transportOperation));
            }
            catch (Exception e)
            {
                var incrementedAttempts = failedMessageRetry.StageAttempts + 1;
                var uniqueMessageId = transportOperation.Message.Headers["ServiceControl.Retry.UniqueMessageId"];

                if (incrementedAttempts < MaxStagingAttempts)
                {
                    Log.Warn($"Attempt {incrementedAttempts} of {MaxStagingAttempts} to stage a retry message {uniqueMessageId} failed", e);

                    await store.IncrementAttemptCounter(failedMessageRetry);
                }
                else
                {
                    Log.Error($"Retry message {uniqueMessageId} reached its staging retry limit ({MaxStagingAttempts}) and is going to be removed from the batch.", e);

                    await store.DeleteFailedMessageRetry(FailedMessageRetry.MakeDocumentId(uniqueMessageId));

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
            if (attempt.MessageMetadata.ContainsKey("Body") || attempt.Body != null)
            {
                headersToRetryWith["ServiceControl.Retry.BodyOnFailedMessage"] = null;
            }

            corruptedReplyToHeaderStrategy.FixCorruptedReplyToHeader(headersToRetryWith);

            var transportMessage = new OutgoingMessage(message.Id, headersToRetryWith, Array.Empty<byte>());
            return new TransportOperation(transportMessage, new UnicastAddressTag(returnToSender.InputAddress));
        }

        IDomainEvents domainEvents;
        IRetryBatchesDataStore store;
        ReturnToSenderDequeuer returnToSender;
        RetryingManager retryingManager;
        MessageRedirectsCollection redirects;
        bool isRecoveringFromPrematureShutdown = true;
        CorruptedReplyToHeaderStrategy corruptedReplyToHeaderStrategy;
        protected internal const int MaxStagingAttempts = 5;

        static ILog Log = LogManager.GetLogger(typeof(RetryProcessor));
    }
}