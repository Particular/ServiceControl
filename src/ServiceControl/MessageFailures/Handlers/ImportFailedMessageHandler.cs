namespace ServiceControl.MessageFailures.Handlers
{
    using System.Linq;
    using Contracts.Operations;
    using NServiceBus;
    using Raven.Client;

    class ImportFailedMessageHandler : IHandleMessages<ImportFailedMessage>
    {
        public IDocumentSession Session { get; set; }

        public void Handle(ImportFailedMessage message)
        {

            var messageId = "FailedMessages/" + message.UniqueMessageId;

            var failure = Session.Load<FailedMessage>(messageId) ?? new FailedMessage
            {
                Id = messageId,
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

            Session.Store(failure);
        }
    }
}