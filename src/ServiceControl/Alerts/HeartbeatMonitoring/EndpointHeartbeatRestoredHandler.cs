namespace ServiceControl.Alerts.HeartbeatMonitoring
{
    using System.Collections.Generic;
    using Contracts.Alerts;
    using Contracts.HeartbeatMonitoring;
    using NServiceBus;
    using Raven.Client;

    public class EndpointHeartbeatRestoredHandler : IHandleMessages<EndpointHeartbeatRestored>
    {
        public IBus Bus { get; set; }
        public IDocumentStore DocumentStore { get; set; }

        public void Handle(EndpointHeartbeatRestored message)
        {
            using (var session = DocumentStore.OpenSession())
            {
                session.Advanced.UseOptimisticConcurrency = true;

                var alert = new Alert
                {
                    RaisedAt = message.RestoredAt,
                    Severity = Severity.Info,
                    Description =
                        "Endpoint heartbeat has been restored.",
                    Category = Category.HeartbeatFailure,
                    RelatedTo = new List<string> { string.Format("endpoint/{0}/{1}", message.Endpoint, message.Machine) }
                };

                session.Store(alert);
                session.SaveChanges();

                Bus.Publish<AlertRaised>(m =>
                {
                    m.RaisedAt = alert.RaisedAt;
                    m.Severity = alert.Severity;
                    m.Description = alert.Description;
                    m.Id = alert.Id;
                    m.Category = alert.Category;
                    m.RelatedTo = alert.RelatedTo;
                });
            }
        }
    }
}
