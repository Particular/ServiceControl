namespace ServiceControl.Operations
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Contracts.MessageFailures;
    using Contracts.Operations;
    using Infrastructure;
    using Infrastructure.DomainEvents;
    using Infrastructure.Metrics;
    using Microsoft.Extensions.Logging;
    using NServiceBus;
    using NServiceBus.Transport;
    using Recoverability;
    using ServiceControl.Persistence;
    using ServiceControl.Persistence.UnitOfWork;

    class ErrorProcessor
    {
        public ErrorProcessor(
            IEnrichImportedErrorMessages[] enrichers,
            IFailedMessageEnricher[] failedMessageEnrichers,
            IDomainEvents domainEvents,
            Counter ingestedCounter,
            ILogger logger)
        {
            this.enrichers = enrichers;
            this.domainEvents = domainEvents;
            this.ingestedCounter = ingestedCounter;
            this.logger = logger;
            failedMessageFactory = new FailedMessageFactory(failedMessageEnrichers);
        }

        public async Task<IReadOnlyList<MessageContext>> Process(IReadOnlyList<MessageContext> contexts, IIngestionUnitOfWork unitOfWork)
        {
            var storedContexts = new List<MessageContext>(contexts.Count);
            var tasks = new List<Task>(contexts.Count);
            foreach (var context in contexts)
            {
                tasks.Add(ProcessMessage(context, unitOfWork));
            }

            await Task.WhenAll(tasks);

            var knownEndpoints = new Dictionary<string, KnownEndpoint>();
            foreach (var context in contexts)
            {
                if (!context.Extensions.TryGet<FailureDetails>(out _) ||
                    // Any message context that failed during processing will have a faulted task and should be skipped
                    context.GetTaskCompletionSource().Task.IsFaulted)
                {
                    continue;
                }

                storedContexts.Add(context);
                ingestedCounter.Mark();

                foreach (var endpointDetail in context.Extensions.Get<IEnumerable<EndpointDetails>>())
                {
                    RecordKnownEndpoints(endpointDetail, knownEndpoints);
                }
            }

            foreach (var endpoint in knownEndpoints.Values)
            {
                logger.LogDebug("Adding known endpoint '{endpointName}' for bulk storage", endpoint.EndpointDetails.Name);

                await unitOfWork.Monitoring.RecordKnownEndpoint(endpoint);
            }

            return storedContexts;
        }

        public Task Announce(MessageContext messageContext)
        {
            var failureDetails = messageContext.Extensions.Get<FailureDetails>();
            var headers = messageContext.Headers;

            var failingEndpointId = headers.ProcessingEndpointName();

            if (headers.TryGetValue("ServiceControl.Retry.UniqueMessageId", out var failedMessageId))
            {
                return domainEvents.Raise(new MessageFailed
                {
                    FailureDetails = failureDetails,
                    EndpointId = failingEndpointId,
                    FailedMessageId = failedMessageId,
                    RepeatedFailure = true
                });
            }

            return domainEvents.Raise(new MessageFailed
            {
                FailureDetails = failureDetails,
                EndpointId = failingEndpointId,
                FailedMessageId = headers.UniqueId()
            });
        }

        async Task ProcessMessage(MessageContext context, IIngestionUnitOfWork unitOfWork)
        {
            bool isOriginalMessageId = true;
            if (!context.Headers.TryGetValue(Headers.MessageId, out var messageId))
            {
                messageId = DeterministicGuid.MakeId(context.NativeMessageId).ToString();
                isOriginalMessageId = false;
            }

            logger.LogDebug("Ingesting error message {nativeMessageId} (original message id: {messageId})", context.NativeMessageId, isOriginalMessageId ? messageId : string.Empty);

            try
            {
                var (metadata, enricherContext) = ExecuteEnrichRoutinesAndCreateMetaData(context, messageId);

                var failureDetails = failedMessageFactory.ParseFailureDetails(context.Headers);

                var processingAttempt = failedMessageFactory.CreateProcessingAttempt(
                    context.Headers,
                    new Dictionary<string, object>(metadata),
                    failureDetails);

                var groups = failedMessageFactory.GetGroups((string)metadata["MessageType"], failureDetails, processingAttempt);

                await unitOfWork.Recoverability.RecordFailedProcessingAttempt(context, processingAttempt, groups);

                context.Extensions.Set(failureDetails);
                context.Extensions.Set(enricherContext.NewEndpoints);
            }
            catch (Exception e)
            {
                logger.LogWarning(e, "Processing of message '{nativeMessageId}' failed.", context.NativeMessageId);

                // releasing the failed message context early so that they can be retried outside the current batch
                context.GetTaskCompletionSource().TrySetException(e);
            }
        }

        (Dictionary<string, object> metadata, ErrorEnricherContext enricherContext) ExecuteEnrichRoutinesAndCreateMetaData(MessageContext context, string messageId)
        {
            var metadata = new Dictionary<string, object>
            {
                ["MessageId"] = messageId,
                ["MessageIntent"] = context.Headers.MessageIntent()
            };
            var enricherContext = new ErrorEnricherContext(context.Headers, metadata);
            foreach (var enricher in enrichers)
            {
                enricher.Enrich(enricherContext);
            }

            return (metadata, enricherContext);
        }

        static void RecordKnownEndpoints(EndpointDetails observedEndpoint, Dictionary<string, KnownEndpoint> observedEndpoints)
        {
            var uniqueEndpointId = $"{observedEndpoint.Name}{observedEndpoint.HostId}";
            if (!observedEndpoints.TryGetValue(uniqueEndpointId, out KnownEndpoint _))
            {
                observedEndpoints.Add(uniqueEndpointId, new KnownEndpoint
                {
                    EndpointDetails = observedEndpoint,
                    HostDisplayName = observedEndpoint.Host,
                    Monitored = false
                });
            }
        }

        readonly IEnrichImportedErrorMessages[] enrichers;
        readonly IDomainEvents domainEvents;
        readonly Counter ingestedCounter;
        readonly FailedMessageFactory failedMessageFactory;
        readonly ILogger logger;
    }
}