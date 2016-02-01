namespace ServiceControl.MessageFailures.Handlers
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using NServiceBus;
    using Raven.Client;
    using ServiceControl.Contracts.MessageFailures;
    using ServiceControl.Contracts.Operations;

    class ImportFailedMessageHandler : IHandleMessages<ImportFailedMessage>
    {
        public IDocumentSession Session { get; set; }
        
        public IEnumerable<IFailedMessageEnricher> Enrichers { get; set; }

        public async Task Handle(ImportFailedMessage message, IMessageHandlerContext context)
        {
            var documentId = FailedMessage.MakeDocumentId(message.UniqueMessageId);

            var failure = Session.Load<FailedMessage>(documentId) ?? new FailedMessage
            {
                Id = documentId,
                UniqueMessageId = message.UniqueMessageId
            };

            failure.Status = FailedMessageStatus.Unresolved;

            var timeOfFailure = message.FailureDetails.TimeOfFailure;


            //check for duplicate
            if (failure.ProcessingAttempts.Any(a => a.AttemptedAt == timeOfFailure))
            {
                return;
            }

            failure.ProcessingAttempts.Add(new FailedMessage.ProcessingAttempt
            {
                AttemptedAt = timeOfFailure,
                FailureDetails = message.FailureDetails,
                MessageMetadata = message.Metadata,
                MessageId = message.PhysicalMessage.MessageId,
                Headers = message.PhysicalMessage.Headers,
                ReplyToAddress = message.PhysicalMessage.ReplyToAddress,
                Recoverable = message.PhysicalMessage.Recoverable,
                CorrelationId = message.PhysicalMessage.CorrelationId,
                MessageIntent = message.PhysicalMessage.MessageIntent,
            });

            foreach (var enricher in Enrichers)
            {
                enricher.Enrich(failure, message);
            }

            Session.Store(failure);

            string failedMessageId;
            if (message.PhysicalMessage.Headers.TryGetValue("ServiceControl.Retry.UniqueMessageId", out failedMessageId))
            {
                await context.Publish<MessageFailedRepeatedly>(m =>
                {
                    m.FailureDetails = message.FailureDetails;
                    m.EndpointId = message.FailingEndpointId;
                    m.FailedMessageId = failedMessageId;
                });
            }
            else
            {
                await context.Publish<MessageFailed>(m =>
                {
                    m.FailureDetails = message.FailureDetails;
                    m.EndpointId = message.FailingEndpointId;
                    m.FailedMessageId = message.UniqueMessageId;
                });
            }
        }
    }
}