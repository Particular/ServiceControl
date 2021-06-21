namespace ServiceControl.Operations
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using Contracts.Operations;
    using Infrastructure.Metrics;
    using NServiceBus.Extensibility;
    using NServiceBus.Logging;
    using NServiceBus.Routing;
    using NServiceBus.Transport;
    using Raven.Client;

    class ErrorIngestor
    {
        public ErrorIngestor(ErrorPersister errorPersister, FailedMessageAnnouncer failedMessageAnnouncer, IDocumentStore store, Meter bulkInsertDurationMeter, bool forwardErrorMessages, string errorLogQueue)
        {
            this.errorPersister = errorPersister;
            this.failedMessageAnnouncer = failedMessageAnnouncer;
            this.store = store;
            this.bulkInsertDurationMeter = bulkInsertDurationMeter;
            this.forwardErrorMessages = forwardErrorMessages;
            this.errorLogQueue = errorLogQueue;
        }

        public async Task Ingest(List<MessageContext> contexts)
        {
            var stored = await Persist(contexts)
                .ConfigureAwait(false);

            try
            {
                var announcerTasks = new List<Task>(stored.Count);
                foreach (var context in stored)
                {
                    announcerTasks.Add(failedMessageAnnouncer.Announce(context.Headers, context.Extensions.Get<FailureDetails>()));
                }

                await Task.WhenAll(announcerTasks).ConfigureAwait(false);

                if (forwardErrorMessages)
                {
                    if (log.IsDebugEnabled)
                    {
                        log.Debug($"Forwarding {contexts.Count} messages");
                    }
                    await Forward(stored).ConfigureAwait(false);
                    if (log.IsDebugEnabled)
                    {
                        log.Debug($"Forwarded messages");
                    }
                }

                foreach (var context in stored)
                {
                    context.GetTaskCompletionSource().TrySetResult(true);
                }
            }
            catch (Exception e)
            {
                if (log.IsDebugEnabled)
                {
                    log.Debug("Forwarding messages failed", e);
                }

                // making sure to rethrow so that all messages get marked as failed
                throw;
            }
        }

        public async Task<IReadOnlyList<MessageContext>> Persist(List<MessageContext> contexts)
        {
            var stopwatch = Stopwatch.StartNew();

            if (log.IsDebugEnabled)
            {
                log.Debug($"Batch size {contexts.Count}");
            }

            try
            {
                var (storedContexts, commands) = await errorPersister.Persist(contexts).ConfigureAwait(false);

                using (bulkInsertDurationMeter.Measure())
                {
                    // not really interested in the batch results since a batch is atomic
                    await store.AsyncDatabaseCommands.BatchAsync(commands)
                        .ConfigureAwait(false);
                }

                return storedContexts;
            }
            catch (Exception e)
            {
                if (log.IsDebugEnabled)
                {
                    log.Debug("Bulk insertion failed", e);
                }

                // making sure to rethrow so that all messages get marked as failed
                throw;
            }
            finally
            {
                stopwatch.Stop();
                if (log.IsDebugEnabled)
                {
                    log.Debug($"Batch size {contexts.Count} took {stopwatch.ElapsedMilliseconds} ms");
                }
            }
        }

        Task Forward(IReadOnlyCollection<MessageContext> messageContexts)
        {
            var transportOperations = new TransportOperation[messageContexts.Count]; //We could allocate based on the actual number of ProcessedMessages but this should be OK
            var index = 0;
            MessageContext anyContext = null;
            foreach (var messageContext in messageContexts)
            {
                anyContext = messageContext;
                var outgoingMessage = new OutgoingMessage(
                    messageContext.MessageId,
                    messageContext.Headers,
                    messageContext.Body);

                // Forwarded messages should last as long as possible
                outgoingMessage.Headers.Remove(NServiceBus.Headers.TimeToBeReceived);

                transportOperations[index] = new TransportOperation(outgoingMessage, new UnicastAddressTag(errorLogQueue));
                index++;
            }

            return anyContext != null
                ? dispatcher.Dispatch(
                    new TransportOperations(transportOperations),
                    anyContext.TransportTransaction,
                    anyContext.Extensions
                )
                : Task.CompletedTask;
        }

        public async Task Initialize(IDispatchMessages dispatcher)
        {
            this.dispatcher = dispatcher;
            if (forwardErrorMessages)
            {
                await VerifyCanReachForwardingAddress().ConfigureAwait(false);
            }
        }

        async Task VerifyCanReachForwardingAddress()
        {
            try
            {
                var transportOperations = new TransportOperations(
                    new TransportOperation(
                        new OutgoingMessage(Guid.Empty.ToString("N"),
                            new Dictionary<string, string>(),
                            new byte[0]),
                        new UnicastAddressTag(errorLogQueue)
                    )
                );

                await dispatcher.Dispatch(transportOperations, new TransportTransaction(), new ContextBag())
                    .ConfigureAwait(false);
            }
            catch (Exception e)
            {
                throw new Exception($"Unable to write to forwarding queue {errorLogQueue}", e);
            }
        }

        bool forwardErrorMessages;
        IDispatchMessages dispatcher;
        string errorLogQueue;
        FailedMessageAnnouncer failedMessageAnnouncer;
        readonly IDocumentStore store;
        readonly Meter bulkInsertDurationMeter;
        ErrorPersister errorPersister;
        static ILog log = LogManager.GetLogger<ErrorIngestor>();
    }
}