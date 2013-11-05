namespace ServiceControl.Alerts.CustomChecks
{
    using System;
    using System.Collections.Generic;
    using Contracts.Alerts;
    using Contracts.CustomChecks;
    using NServiceBus;
    using Raven.Client;

    class CustomCheckSucceededHandler : IHandleMessages<CustomCheckSucceeded>
    {
        public IBus Bus { get; set; }
        public IDocumentStore DocumentStore { get; set; }

        public void Handle(CustomCheckSucceeded message)
        {
            using (var session = DocumentStore.OpenSession())
            {
                session.Advanced.UseOptimisticConcurrency = true;

                var alert = new Alert
                {
                    RaisedAt = message.SucceededAt,
                    Severity = Severity.Info,
                    Description = String.Format("{0}: Working as expected.", message.CustomCheckId),
                    Tags = String.Format("{0}, {1}", Category.CustomChecks, message.Category),
                    Category = message.Category,
                    RelatedTo = new List<string> { String.Format("endpoint/{0}/{1}", message.OriginatingEndpoint.Name, message.OriginatingEndpoint.Machine) }
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
                    m.Tags = alert.Tags;
                });
            }
        }
    }
}
