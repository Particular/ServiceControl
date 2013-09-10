namespace ServiceControl.Alerts
{
    using System;
    using Contracts.Alerts;
    using Contracts.HeartbeatMonitoring;
    using Raven.Abstractions.Exceptions;
    using Raven.Client;

    using NServiceBus;

    class EndpointFailedHeartbeatHandler : IHandleMessages<EndpointFailedToHeartbeat>
    {
        public IBus Bus { get; set; }
        public IDocumentStore DocumentStore { get; set; }

        public void Handle(EndpointFailedToHeartbeat message)
        {
            using (var session = DocumentStore.OpenSession())
            {
                session.Advanced.UseOptimisticConcurrency = true;

                var alertRaised = new AlertRaised()
                {
                    RaisedAt = DateTime.Now,
                    Endpoint = message.Endpoint,
                    Machine = message.Machine,
                    Severity = Severity.Error,
                    Description =
                        "Endpoint has failed to send expected heartbeats to ServiceControl. It is possible that the endpoint could be down or is unresponsive. If this condition persists, you might want to restart your endpoint.",
                    Type = message.GetType().FullName,
                    RelatedId = null
                };

                try
                {
                    session.Store(alertRaised);
                    session.SaveChanges();
                    Bus.Publish(alertRaised);
                }
                catch (ConcurrencyException) //there is already a message in the store with the same id
                {
                    session.Advanced.Clear();
                }
            }
        }
    }
}
