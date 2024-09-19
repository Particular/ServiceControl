﻿namespace ServiceControl.Operations
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading.Tasks;
    using Contracts.Operations;
    using Infrastructure.DomainEvents;
    using Infrastructure.Metrics;
    using NServiceBus.Logging;
    using NServiceBus.Routing;
    using NServiceBus.Transport;
    using Recoverability;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Persistence.UnitOfWork;

    public class ErrorIngestor
    {
        static readonly long FrequencyInMilliseconds = Stopwatch.Frequency / 1000;

        public ErrorIngestor(Metrics metrics,
            IEnumerable<IEnrichImportedErrorMessages> errorEnrichers,
            IEnumerable<IFailedMessageEnricher> failedMessageEnrichers,
            IDomainEvents domainEvents,
            IIngestionUnitOfWorkFactory unitOfWorkFactory,
            Lazy<IMessageDispatcher> messageDispatcher,
            Settings settings)
        {
            this.unitOfWorkFactory = unitOfWorkFactory;
            this.messageDispatcher = messageDispatcher;
            this.settings = settings;

            bulkInsertDurationMeter = metrics.GetMeter("Error ingestion - bulk insert duration", FrequencyInMilliseconds);
            var ingestedMeter = metrics.GetCounter("Error ingestion - ingested");

            var enrichers = new IEnrichImportedErrorMessages[]
            {
                new MessageTypeEnricher(),
                new EnrichWithTrackingIds(),
                new ProcessingStatisticsEnricher()

            }.Concat(errorEnrichers).ToArray();

            errorProcessor = new ErrorProcessor(enrichers, failedMessageEnrichers.ToArray(), domainEvents, ingestedMeter);
            retryConfirmationProcessor = new RetryConfirmationProcessor(domainEvents);
        }

        public async Task Ingest(List<MessageContext> contexts)
        {
            var failedMessages = new List<MessageContext>(contexts.Count);
            var retriedMessages = new List<MessageContext>(contexts.Count);

            foreach (var context in contexts)
            {
                if (context.Headers.ContainsKey(ConnectedApplicationsTracker.ConnectedApplicationControlHeader))
                {
                    settings.ConnectedApplications.Add(context.Headers[ConnectedApplicationsTracker.ConnectedApplicationControlHeader]);
                }
                else if (context.Headers.ContainsKey(RetryConfirmationProcessor.SuccessfulRetryHeader))
                {
                    retriedMessages.Add(context);
                }
                else
                {
                    failedMessages.Add(context);
                }
            }

            var storedFailed = await PersistFailedMessages(failedMessages, retriedMessages);

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
                    if (Logger.IsDebugEnabled)
                    {
                        Logger.Debug($"Forwarding {storedFailed.Count} messages");
                    }
                    await Forward(storedFailed);
                    if (Logger.IsDebugEnabled)
                    {
                        Logger.Debug("Forwarded messages");
                    }
                }

                foreach (var context in contexts)
                {
                    context.GetTaskCompletionSource().TrySetResult(true);
                }
            }
            catch (Exception e)
            {
                if (Logger.IsWarnEnabled)
                {
                    Logger.Warn("Forwarding messages failed", e);
                }

                // making sure to rethrow so that all messages get marked as failed
                throw;
            }
        }

        async Task<IReadOnlyList<MessageContext>> PersistFailedMessages(List<MessageContext> failedMessageContexts, List<MessageContext> retriedMessageContexts)
        {
            var stopwatch = Stopwatch.StartNew();

            if (Logger.IsDebugEnabled)
            {
                Logger.Debug($"Batch size {failedMessageContexts.Count}");
            }

            try
            {
                using var unitOfWork = await unitOfWorkFactory.StartNew();
                var storedFailedMessageContexts = await errorProcessor.Process(failedMessageContexts, unitOfWork);
                await retryConfirmationProcessor.Process(retriedMessageContexts, unitOfWork);

                using (bulkInsertDurationMeter.Measure())
                {
                    await unitOfWork.Complete();
                }
                return storedFailedMessageContexts;
            }
            catch (Exception e)
            {
                if (Logger.IsWarnEnabled)
                {
                    Logger.Warn("Bulk insertion failed", e);
                }

                // making sure to rethrow so that all messages get marked as failed
                throw;
            }
            finally
            {
                stopwatch.Stop();
                if (Logger.IsDebugEnabled)
                {
                    Logger.Debug($"Batch size {failedMessageContexts.Count} took {stopwatch.ElapsedMilliseconds} ms");
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
                    messageContext.NativeMessageId,
                    messageContext.Headers,
                    messageContext.Body);

                // Forwarded messages should last as long as possible
                outgoingMessage.Headers.Remove(NServiceBus.Headers.TimeToBeReceived);

                transportOperations[index] = new TransportOperation(outgoingMessage, new UnicastAddressTag(settings.ErrorLogQueue));
                index++;
            }

            return anyContext != null
                ? messageDispatcher.Value.Dispatch(
                    new TransportOperations(transportOperations),
                    anyContext.TransportTransaction)
                : Task.CompletedTask;
        }

        public async Task VerifyCanReachForwardingAddress()
        {
            try
            {
                var transportOperations = new TransportOperations(
                    new TransportOperation(
                        new OutgoingMessage(Guid.Empty.ToString("N"),
                            [], Array.Empty<byte>()),
                        new UnicastAddressTag(settings.ErrorLogQueue)
                    )
                );

                await messageDispatcher.Value.Dispatch(transportOperations, new TransportTransaction());
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
        static readonly ILog Logger = LogManager.GetLogger<ErrorIngestor>();
    }
}