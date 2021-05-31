namespace ServiceControl.Recoverability
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Infrastructure.DomainEvents;
    using MessageFailures;
    using MessageRedirects;
    using NServiceBus.Extensibility;
    using NServiceBus.Logging;
    using NServiceBus.Routing;
    using NServiceBus.Support;
    using NServiceBus.Transport;
    using Raven.Abstractions.Commands;
    using Raven.Abstractions.Data;
    using Raven.Abstractions.Exceptions;
    using Raven.Client;

    class RetryProcessor
    {
        public RetryProcessor(IDocumentStore store, IDispatchMessages sender, IDomainEvents domainEvents, ReturnToSenderDequeuer returnToSender, RetryingManager retryingManager)
        {
            this.store = store;
            this.sender = sender;
            this.returnToSender = returnToSender;
            this.retryingManager = retryingManager;
            this.domainEvents = domainEvents;
            corruptedReplyToHeaderStrategy = new CorruptedReplyToHeaderStrategy(RuntimeEnvironment.MachineName);
        }

        public async Task<bool> ProcessBatches(IAsyncDocumentSession session, CancellationToken cancellationToken = default)
        {
            return await ForwardCurrentBatch(session, cancellationToken).ConfigureAwait(false) || await MoveStagedBatchesToForwardingBatch(session).ConfigureAwait(false);
        }

        async Task<bool> MoveStagedBatchesToForwardingBatch(IAsyncDocumentSession session)
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

        async Task<bool> ForwardCurrentBatch(IAsyncDocumentSession session, CancellationToken cancellationToken)
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
                    if (Log.IsDebugEnabled)
                    {
                        Log.Info($"Forwarding batch {forwardingBatch.Id}.");
                    }

                    await Forward(forwardingBatch, session, cancellationToken)
                        .ConfigureAwait(false);

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
                await returnToSender.Run(forwardingBatch.Id, IsPartOfStagedBatch(forwardingBatch.StagingId), null, cancellationToken)
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
                    await returnToSender.Run(forwardingBatch.Id, IsPartOfStagedBatch(forwardingBatch.StagingId), messageCount, cancellationToken)
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

            var failedMessageRetriesById = failedMessageRetryDocs
                .Where(r => r != null && r.RetryBatchId == stagingBatch.Id)
                .Distinct(FailedMessageEqualityComparer.Instance)
                .ToDictionary(x => x.FailedMessageId, x => x);

            foreach (var failedMessageRetry in failedMessageRetryDocs)
            {
                if (failedMessageRetry != null)
                {
                    session.Advanced.Evict(failedMessageRetry);
                }
            }

            if (failedMessageRetriesById.Count == 0)
            {
                Log.Info($"Retry batch {stagingBatch.Id} cancelled as all matching unresolved messages are already marked for retry as part of another batch.");
                session.Delete(stagingBatch);
                return 0;
            }

            var failedMessagesDocs = await session.LoadAsync<FailedMessage>(failedMessageRetriesById.Keys).ConfigureAwait(false);
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

            await TryDispatch(transportOperations, messages, failedMessageRetriesById, stagingId, previousAttemptFailed).ConfigureAwait(false);

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
            stagingBatch.FailureRetries = failedMessageRetriesById.Values.Where(x => msgLookup[x.FailedMessageId].Any()).Select(x => x.Id).ToArray();
            Log.Info($"Retry batch {stagingBatch.Id} staged with Staging Id {stagingBatch.StagingId} and {stagingBatch.FailureRetries.Count} matching failure retries");
            return messages.Length;
        }

        Task TryDispatch(TransportOperation[] transportOperations, IReadOnlyCollection<FailedMessage> messages, IReadOnlyDictionary<string, FailedMessageRetry> failedMessageRetriesById, string stagingId, bool previousAttemptFailed)
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

        async Task BatchDispatchToTransport(TransportOperation[] transportOperations, IReadOnlyCollection<FailedMessage> messages, IReadOnlyDictionary<string, FailedMessageRetry> failedMessageRetriesById, string stagingId)
        {
            try
            {
                await sender.Dispatch(new TransportOperations(transportOperations), transaction, contextBag).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                var commands = new ICommandData[messages.Count];
                var commandIndex = 0;
                foreach (var failedMessage in messages)
                {
                    var failedMessageRetry = failedMessageRetriesById[failedMessage.Id];

                    Log.Warn($"Attempt {1} of {MaxStagingAttempts} to stage a retry message {failedMessage.UniqueMessageId} failed", e);

                    commands[commandIndex] = new PatchCommandData
                    {
                        Patches = new[]
                        {
                            new PatchRequest
                            {
                                Type = PatchCommandType.Set,
                                Name = "StageAttempts",
                                Value = 1
                            }
                        },
                        Key = failedMessageRetry.Id
                    };

                    commandIndex++;
                }

                try
                {
                    await store.AsyncDatabaseCommands.BatchAsync(commands).ConfigureAwait(false);
                }
                catch (ConcurrencyException)
                {
                    Log.DebugFormat("Ignoring concurrency exception while incrementing staging attempt count for {0}", stagingId);
                }

                throw new RetryStagingException(e);
            }
        }

        async Task TryStageMessage(TransportOperation transportOperation, FailedMessageRetry failedMessageRetry)
        {
            try
            {
                await sender.Dispatch(new TransportOperations(transportOperation), transaction, contextBag).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                var incrementedAttempts = failedMessageRetry.StageAttempts + 1;
                var uniqueMessageId = transportOperation.Message.Headers["ServiceControl.Retry.UniqueMessageId"];

                if (incrementedAttempts < MaxStagingAttempts)
                {
                    Log.Warn($"Attempt {incrementedAttempts} of {MaxStagingAttempts} to stage a retry message {uniqueMessageId} failed", e);

                    await IncrementAttemptCounter(failedMessageRetry)
                        .ConfigureAwait(false);
                }
                else
                {
                    Log.Error($"Retry message {uniqueMessageId} reached its staging retry limit ({MaxStagingAttempts}) and is going to be removed from the batch.", e);

                    await store.AsyncDatabaseCommands.DeleteAsync(FailedMessageRetry.MakeDocumentId(uniqueMessageId), null)
                        .ConfigureAwait(false);

                    await domainEvents.Raise(new MessageFailedInStaging
                    {
                        UniqueMessageId = uniqueMessageId
                    }).ConfigureAwait(false);
                }

                throw new RetryStagingException(e);
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
        protected internal const int MaxStagingAttempts = 5;

        static ILog Log = LogManager.GetLogger(typeof(RetryProcessor));
    }
}