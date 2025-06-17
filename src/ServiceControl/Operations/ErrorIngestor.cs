namespace ServiceControl.Operations
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Contracts.Operations;
    using Infrastructure.DomainEvents;
    using Infrastructure.Metrics;
    using Microsoft.Extensions.Logging;
    using NServiceBus.Routing;
    using NServiceBus.Transport;
    using Recoverability;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Persistence.UnitOfWork;
    using ServiceControl.Transports;

    public class ErrorIngestor
    {
        static readonly long FrequencyInMilliseconds = Stopwatch.Frequency / 1000;

        public ErrorIngestor(Metrics metrics,
            IEnumerable<IEnrichImportedErrorMessages> errorEnrichers,
            IEnumerable<IFailedMessageEnricher> failedMessageEnrichers,
            IDomainEvents domainEvents,
            IIngestionUnitOfWorkFactory unitOfWorkFactory,
            Lazy<IMessageDispatcher> messageDispatcher,
            ITransportCustomization transportCustomization,
            Settings settings,
            ILogger<ErrorIngestor> logger)
        {
            this.unitOfWorkFactory = unitOfWorkFactory;
            this.messageDispatcher = messageDispatcher;
            this.settings = settings;
            this.logger = logger;
            bulkInsertDurationMeter = metrics.GetMeter("Error ingestion - bulk insert duration", FrequencyInMilliseconds);
            var ingestedMeter = metrics.GetCounter("Error ingestion - ingested");

            var enrichers = new IEnrichImportedErrorMessages[]
            {
                new MessageTypeEnricher(),
                new EnrichWithTrackingIds(),
                new ProcessingStatisticsEnricher()

            }.Concat(errorEnrichers).ToArray();

            errorProcessor = new ErrorProcessor(enrichers, failedMessageEnrichers.ToArray(), domainEvents, ingestedMeter, logger);
            retryConfirmationProcessor = new RetryConfirmationProcessor(domainEvents);
            logQueueAddress = new UnicastAddressTag(transportCustomization.ToTransportQualifiedQueueName(this.settings.ErrorLogQueue));
        }

        public async Task Ingest(List<MessageContext> contexts, CancellationToken cancellationToken)
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


            var storedFailed = await PersistFailedMessages(failedMessages, retriedMessages, cancellationToken);

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

                await Task.WhenAll(announcerTasks);

                if (settings.ForwardErrorMessages)
                {
                    logger.LogDebug("Forwarding {FailedMessageCount} messages", storedFailed.Count);

                    await Forward(storedFailed, cancellationToken);

                    logger.LogDebug("Forwarded messages");
                }

                foreach (var context in contexts)
                {
                    context.GetTaskCompletionSource().TrySetResult(true);
                }
            }
            catch (Exception e)
            {
                logger.LogWarning(e, "Forwarding messages failed");

                // making sure to rethrow so that all messages get marked as failed
                throw;
            }
        }

        async Task<IReadOnlyList<MessageContext>> PersistFailedMessages(List<MessageContext> failedMessageContexts, List<MessageContext> retriedMessageContexts, CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();

            logger.LogDebug("Batch size {FailedMessageBatchSize}", failedMessageContexts.Count);

            try
            {
                using var unitOfWork = await unitOfWorkFactory.StartNew();
                var storedFailedMessageContexts = await errorProcessor.Process(failedMessageContexts, unitOfWork);
                await retryConfirmationProcessor.Process(retriedMessageContexts, unitOfWork);

                using (bulkInsertDurationMeter.Measure())
                {
                    await unitOfWork.Complete(cancellationToken);
                }
                return storedFailedMessageContexts;
            }
            catch (Exception e)
            {
                logger.LogWarning(e, "Bulk insertion failed");

                // making sure to rethrow so that all messages get marked as failed
                throw;
            }
            finally
            {
                stopwatch.Stop();

                logger.LogDebug("Batch size {FailedMessageBatchSize} took {FailedMessageBatchProcessingTime} ms", failedMessageContexts.Count, stopwatch.ElapsedMilliseconds);
            }
        }

        Task Forward(IReadOnlyCollection<MessageContext> messageContexts, CancellationToken cancellationToken)
        {
            var transportOperations = new TransportOperation[messageContexts.Count]; //We could allocate based on the actual number of ProcessedMessages but this should be OK
            var index = 0;
            MessageContext anyContext = null;
            foreach (var messageContext in messageContexts)
            {
                anyContext = messageContext;
                var outgoingMessage = new OutgoingMessage(
                    messageContext.NativeMessageId,
                    messageContext.Headers,
                    messageContext.Body);

                // Forwarded messages should last as long as possible
                outgoingMessage.Headers.Remove(NServiceBus.Headers.TimeToBeReceived);

                transportOperations[index] = new TransportOperation(outgoingMessage, logQueueAddress);
                index++;
            }

            return anyContext != null
                ? messageDispatcher.Value.Dispatch(
                    new TransportOperations(transportOperations),
                    anyContext.TransportTransaction, cancellationToken)
                : Task.CompletedTask;
        }

        public async Task VerifyCanReachForwardingAddress(CancellationToken cancellationToken)
        {
            try
            {
                var transportOperations = new TransportOperations(
                    new TransportOperation(
                        new OutgoingMessage(Guid.Empty.ToString("N"),
                            [], Array.Empty<byte>()),
                        logQueueAddress
                    )
                );

                await messageDispatcher.Value.Dispatch(transportOperations, new TransportTransaction(), cancellationToken);
            }
            catch (Exception e)
            {
                throw new Exception($"Unable to write to forwarding queue {settings.ErrorLogQueue}", e);
            }
        }

        readonly IIngestionUnitOfWorkFactory unitOfWorkFactory;
        readonly Meter bulkInsertDurationMeter;
        readonly Settings settings;
        readonly ErrorProcessor errorProcessor;
        readonly Lazy<IMessageDispatcher> messageDispatcher;
        readonly RetryConfirmationProcessor retryConfirmationProcessor;
        readonly UnicastAddressTag logQueueAddress;

        readonly ILogger<ErrorIngestor> logger;
    }
}