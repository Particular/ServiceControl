namespace ServiceControl.Alerts.HeartbeatMonitoring
{
    using System.Collections.Generic;
    using Contracts.Alerts;
    using Contracts.HeartbeatMonitoring;
    using NServiceBus;
    using Raven.Client;

    class EndpointFailedHeartbeatHandler : IHandleMessages<EndpointFailedToHeartbeat>
    {
        public IBus Bus { get; set; }
        public IDocumentStore DocumentStore { get; set; }

        public void Handle(EndpointFailedToHeartbeat message)
        {
            using (var session = DocumentStore.OpenSession())
            {
                session.Advanced.UseOptimisticConcurrency = true;

                var alert = new Alert
                {
                    RaisedAt = message.LastReceivedAt,
                    Severity = Severity.Error,
                    Description =
                        "Endpoint has failed to send expected heartbeat to ServiceControl. It is possible that the endpoint could be down or is unresponsive. If this condition persists, you might want to restart your endpoint.",
                    Category = Category.HeartbeatFailure,
                    RelatedTo = new List<string>(){string.Format("endpoint/{0}/{1}",message.Endpoint, message.Machine)}
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
