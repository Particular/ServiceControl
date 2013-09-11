namespace ServiceControl.Alerts
{
    using Contracts.MessageFailures;
    using NServiceBus;
    using Raven.Client;
    using Contracts.Alerts;

    class MessageFailedHandler : IHandleMessages<MessageFailed>
    {
        public IBus Bus { get; set; }
        public IDocumentStore DocumentStore { get; set; }

        public void Handle(MessageFailed message)
        {
            using (var session = DocumentStore.OpenSession())
            {
                session.Advanced.UseOptimisticConcurrency = true;

                var alert = new Alert()
                {
                    RaisedAt = message.FailedAt,
                    Severity = Severity.Error,
                    Description = string.Format("This message processing failed due to: {0}", message.Reason),
                    Type = message.GetType().FullName,
                    RelatedTo = string.Format("/failedMessageId/{0}",message.Id)
                };

                session.Store(alert);
                session.SaveChanges();

                Bus.Publish<AlertRaised>(m =>
                {
                    m.RaisedAt = alert.RaisedAt;
                    m.Severity = alert.Severity;
                    m.Description = alert.Description;
                    m.Id = alert.Id;
                    m.RelatedTo = alert.RelatedTo;
                    m.Type = alert.Type;
                });
            }
        }
    }
}
