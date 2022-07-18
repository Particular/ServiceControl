namespace ServiceControl.Operations
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using BodyStorage;
    using Contracts.MessageFailures;
    using Contracts.Operations;
    using Infrastructure;
    using Infrastructure.DomainEvents;
    using Infrastructure.Metrics;
    using Monitoring;
    using NServiceBus;
    using NServiceBus.Logging;
    using NServiceBus.Transport;
    using Recoverability;


    class ErrorProcessor
    {
        public ErrorProcessor(BodyStorageEnricher bodyStorageEnricher, IEnrichImportedErrorMessages[] enrichers, IFailedMessageEnricher[] failedMessageEnrichers, IDomainEvents domainEvents,
            Counter ingestedCounter)
        {
            this.bodyStorageEnricher = bodyStorageEnricher;
            this.enrichers = enrichers;
            this.domainEvents = domainEvents;
            this.ingestedCounter = ingestedCounter;
            failedMessageFactory = new FailedMessageFactory(failedMessageEnrichers);
        }

        public async Task<IReadOnlyList<MessageContext>> Process(List<MessageContext> contexts, IIngestionUnitOfWork unitOfWork)
        {
            var storedContexts = new List<MessageContext>(contexts.Count);
            var tasks = new List<Task>(contexts.Count);
            foreach (var context in contexts)
            {
                tasks.Add(ProcessMessage(context, unitOfWork));
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);

            var knownEndpoints = new Dictionary<string, KnownEndpoint>();
            foreach (var context in contexts)
            {
                if (!context.Extensions.TryGet<FailureDetails>(out _))
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
                if (Logger.IsDebugEnabled)
                {
                    Logger.Debug($"Adding known endpoint '{endpoint.EndpointDetails.Name}' for bulk storage");
                }

                unitOfWork.RecordKnownEndpoint(endpoint);
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
                messageId = DeterministicGuid.MakeId(context.MessageId).ToString();
                isOriginalMessageId = false;
            }

            if (Logger.IsDebugEnabled)
            {
                Logger.Debug($"Ingesting error message {context.MessageId} (original message id: {(isOriginalMessageId ? messageId : string.Empty)})");
            }

            try
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

                var failureDetails = failedMessageFactory.ParseFailureDetails(context.Headers);

                var processingAttempt = failedMessageFactory.CreateProcessingAttempt(
                    context.Headers,
                    new Dictionary<string, object>(metadata),
                    failureDetails);

                await bodyStorageEnricher.StoreErrorMessageBody(context.Body, processingAttempt)
                    .ConfigureAwait(false);

                var groups = failedMessageFactory.GetGroups((string)metadata["MessageType"], failureDetails, processingAttempt);

                unitOfWork.RecordFailedProcessingAttempt(context.Headers.UniqueId(), processingAttempt, groups);

                context.Extensions.Set(failureDetails);
                context.Extensions.Set(enricherContext.NewEndpoints);
            }
            catch (Exception e)
            {
                if (Logger.IsDebugEnabled)
                {
                    Logger.Debug($"Processing of message '{context.MessageId}' failed.", e);
                }

                context.GetTaskCompletionSource().TrySetException(e);
            }
        }

        static void RecordKnownEndpoints(EndpointDetails observedEndpoint, Dictionary<string, KnownEndpoint> observedEndpoints)
        {
            var uniqueEndpointId = $"{observedEndpoint.Name}{observedEndpoint.HostId}";
            if (!observedEndpoints.TryGetValue(uniqueEndpointId, out KnownEndpoint _))
            {
                observedEndpoints.Add(uniqueEndpointId, new KnownEndpoint
                {
                    Id = DeterministicGuid.MakeId(observedEndpoint.Name, observedEndpoint.HostId.ToString()),
                    EndpointDetails = observedEndpoint,
                    HostDisplayName = observedEndpoint.Host,
                    Monitored = false
                });
            }
        }

        readonly IEnrichImportedErrorMessages[] enrichers;
        readonly IDomainEvents domainEvents;
        readonly Counter ingestedCounter;
        BodyStorageEnricher bodyStorageEnricher;
        FailedMessageFactory failedMessageFactory;
        static readonly ILog Logger = LogManager.GetLogger<ErrorProcessor>();
    }
}