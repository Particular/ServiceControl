namespace ServiceControl.MessageFailures.Handlers
{
    using System.Linq;
    using NServiceBus;
    using Raven.Client;
    using ServiceControl.Contracts.MessageFailures;
    using ServiceControl.Contracts.Operations;

    class ImportFailedMessageHandler
    {
        private readonly IDocumentStore store;
        private readonly IBus bus;
        private readonly IFailedMessageEnricher[] enrichers;

        public ImportFailedMessageHandler(IDocumentStore store, IBus bus, IFailedMessageEnricher[] enrichers)
        {
            this.store = store;
            this.bus = bus;
            this.enrichers = enrichers;
        }
        
        public void Handle(ImportFailedMessage message)
        {
            var documentId = FailedMessage.MakeDocumentId(message.UniqueMessageId);

            using (var session = store.OpenSession())
            {
                session.Advanced.UseOptimisticConcurrency = true;

                var failure = session.Load<FailedMessage>(documentId) ?? new FailedMessage
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

                foreach (var enricher in enrichers)
                {
                    enricher.Enrich(failure, message);
                }

                session.Store(failure);
                session.SaveChanges();

                string failedMessageId;
                if (message.PhysicalMessage.Headers.TryGetValue("ServiceControl.Retry.UniqueMessageId", out failedMessageId))
                {
                    bus.Publish<MessageFailedRepeatedly>(m =>
                    {
                        m.FailureDetails = message.FailureDetails;
                        m.EndpointId = message.FailingEndpointId;
                        m.FailedMessageId = failedMessageId;
                    });
                }
                else
                {
                    bus.Publish<MessageFailed>(m =>
                    {
                        m.FailureDetails = message.FailureDetails;
                        m.EndpointId = message.FailingEndpointId;
                        m.FailedMessageId = message.UniqueMessageId;
                    });
                }
            }
        }
    }
}