namespace ServiceControl.MessageAuditing.Handlers
{
    using Contracts.Operations;
    using NServiceBus;
    using Raven.Client;

    class ImportMessageHandler 
        : IHandleMessages<ImportSuccessfullyProcessedMessage>,
        IHandleMessages<ImportFailedMessage>
    {
        public IDocumentSession Session { get; set; }

        public void Handle(ImportSuccessfullyProcessedMessage message)
        {
            var auditMessage = new AuditProcessedMessage(message);

            Session.Store(auditMessage);
        }

        public void Handle(ImportFailedMessage message)
        {
            var documentId = AuditFailedMessage.MakeDocumentId(message.UniqueMessageId);

            var failure = Session.Load<AuditFailedMessage>(documentId) ?? new AuditFailedMessage
            {
                Id = documentId,
                UniqueMessageId = message.UniqueMessageId
            };

            failure.Status = failure.LastProcessingAttempt == null 
                ? MessageStatus.Failed
                : MessageStatus.RepeatedFailure;

            var timeOfFailure = message.FailureDetails.TimeOfFailure;

            //ingore if we have the most recent failure
            if (failure.LastProcessingAttempt != null && failure.LastProcessingAttempt.AttemptedAt >= timeOfFailure)
            {
                return;
            }

            failure.LastProcessingAttempt = new AuditFailedMessage.ProcessingAttempt
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
            };
            Session.Store(failure);
        }
    }
}
