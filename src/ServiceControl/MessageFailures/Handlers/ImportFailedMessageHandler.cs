namespace ServiceControl.MessageFailures.Handlers
{
    using System.Collections.Generic;
    using System.Linq;
    using NServiceBus;
    using Raven.Client;
    using ServiceControl.Contracts.Operations;

    class ImportFailedMessageHandler : IHandleMessages<ImportFailedMessage>
    {
        public IDocumentSession Session { get; set; }
        
        public IEnumerable<IFailedMessageEnricher> Enrichers { get; set; } 

        public void Handle(ImportFailedMessage message)
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
        }
    }
}