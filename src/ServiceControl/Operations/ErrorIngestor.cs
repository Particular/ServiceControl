namespace ServiceControl.Operations
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading.Tasks;
    using BodyStorage;
    using Contracts.Operations;
    using Infrastructure.DomainEvents;
    using Infrastructure.Metrics;
    using NServiceBus.Extensibility;
    using NServiceBus.Logging;
    using NServiceBus.Routing;
    using NServiceBus.Transport;
    using Raven.Client;
    using Recoverability;
    using ServiceBus.Management.Infrastructure.Settings;

    class ErrorIngestor
    {
        static readonly long FrequencyInMilliseconds = Stopwatch.Frequency / 1000;

        public ErrorIngestor(Metrics metrics,
            IEnumerable<IEnrichImportedErrorMessages> errorEnrichers,
            IFailedMessageEnricher[] failedMessageEnrichers,
            IErrorMessageBatchPlugin[] batchPlugins,
            IDomainEvents domainEvents,
            IBodyStorage bodyStorage,
            IDocumentStore store, Settings settings)
        {
            this.store = store;
            this.settings = settings;

            bulkInsertDurationMeter = metrics.GetMeter("Error ingestion - bulk insert duration", FrequencyInMilliseconds);
            var ingestedMeter = metrics.GetCounter("Error ingestion - ingested");

            var enrichers = new IEnrichImportedErrorMessages[]
            {
                new MessageTypeEnricher(),
                new EnrichWithTrackingIds(),
                new ProcessingStatisticsEnricher()

            }.Concat(errorEnrichers).ToArray();

            var bodyStorageEnricher = new BodyStorageEnricher(bodyStorage, settings);
            errorProcessor = new ErrorProcessor(bodyStorageEnricher, enrichers, failedMessageEnrichers, domainEvents, ingestedMeter, batchPlugins);
            retryConfirmationProcessor = new RetryConfirmationProcessor(domainEvents);
        }

        public async Task Ingest(List<MessageContext> contexts, IDispatchMessages dispatcher)
        {
            var failedMessages = new List<MessageContext>(contexts.Count);
            var retriedMessages = new List<MessageContext>(contexts.Count);

            foreach (var context in contexts)
            {
                if (context.Headers.ContainsKey(RetryConfirmationProcessor.SuccessfulRetryHeader))
                {
                    retriedMessages.Add(context);
                }
                else
                {
                    failedMessages.Add(context);
                }
            }


            var storedFailed = await PersistFailedMessages(failedMessages, retriedMessages)
                .ConfigureAwait(false);

            try
            {
                var announcerTasks = new List<Task>(contexts.Count);
                foreach (var context in storedFailed)
                {
                    announcerTasks.Add(errorProcessor.Announce(context));
                }
                foreach (var context in retriedMessages)
                {
                    announcerTasks.Add(retryConfirmationProcessor.Announce(context));
                }

                await Task.WhenAll(announcerTasks).ConfigureAwait(false);

                if (settings.ForwardErrorMessages)
                {
                    if (log.IsDebugEnabled)
                    {
                        log.Debug($"Forwarding {contexts.Count} messages");
                    }
                    await Forward(storedFailed, dispatcher).ConfigureAwait(false);
                    if (log.IsDebugEnabled)
                    {
                        log.Debug("Forwarded messages");
                    }
                }

                foreach (var context in storedFailed.Concat(retriedMessages))
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

        async Task<IReadOnlyList<MessageContext>> PersistFailedMessages(List<MessageContext> failedMessageContexts, List<MessageContext> retriedMessageContexts)
        {
            var stopwatch = Stopwatch.StartNew();

            if (log.IsDebugEnabled)
            {
                log.Debug($"Batch size {failedMessageContexts.Count}");
            }

            try
            {
                var (storedFailedMessageContexts, storeCommands) = await errorProcessor.Process(failedMessageContexts).ConfigureAwait(false);
                var markRetriedCommands = retryConfirmationProcessor.Process(retriedMessageContexts);

                using (bulkInsertDurationMeter.Measure())
                {
                    // not really interested in the batch results since a batch is atomic
                    var allCommands = storeCommands.Concat(markRetriedCommands);
                    await store.AsyncDatabaseCommands.BatchAsync(allCommands)
                        .ConfigureAwait(false);
                }

                return storedFailedMessageContexts;
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
                    log.Debug($"Batch size {failedMessageContexts.Count} took {stopwatch.ElapsedMilliseconds} ms");
                }
            }
        }

        Task Forward(IReadOnlyCollection<MessageContext> messageContexts, IDispatchMessages dispatcher)
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

                transportOperations[index] = new TransportOperation(outgoingMessage, new UnicastAddressTag(settings.ErrorLogQueue));
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

        public async Task VerifyCanReachForwardingAddress(IDispatchMessages dispatcher)
        {
            try
            {
                var transportOperations = new TransportOperations(
                    new TransportOperation(
                        new OutgoingMessage(Guid.Empty.ToString("N"),
                            new Dictionary<string, string>(),
                            new byte[0]),
                        new UnicastAddressTag(settings.ErrorLogQueue)
                    )
                );

                await dispatcher.Dispatch(transportOperations, new TransportTransaction(), new ContextBag())
                    .ConfigureAwait(false);
            }
            catch (Exception e)
            {
                throw new Exception($"Unable to write to forwarding queue {settings.ErrorLogQueue}", e);
            }
        }

        readonly IDocumentStore store;
        readonly Meter bulkInsertDurationMeter;
        readonly Settings settings;
        ErrorProcessor errorProcessor;
        readonly RetryConfirmationProcessor retryConfirmationProcessor;
        static ILog log = LogManager.GetLogger<ErrorIngestor>();
    }
}