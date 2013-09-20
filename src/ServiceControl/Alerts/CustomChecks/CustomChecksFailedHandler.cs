namespace ServiceControl.Alerts.CustomChecks
{
    using System.Collections.Generic;
    using Contracts.Alerts;
    using Contracts.CustomChecks;
    using NServiceBus;
    using Raven.Client;

    class CustomChecksFailedHandler : IHandleMessages<CustomCheckFailed>
    {
        public IBus Bus { get; set; }
        public IDocumentStore DocumentStore { get; set; }

        public void Handle(CustomCheckFailed message)
        {
            using (var session = DocumentStore.OpenSession())
            {
                session.Advanced.UseOptimisticConcurrency = true;

                var alert = new Alert()
                {
                    RaisedAt = message.FailedAt,
                    Severity = Severity.Error,
                    Description = string.Format("{0} failed. Reason: {1}", message.CustomCheckId, message.FailureReason),
                    Tags = string.Format("{0}, {1}", Category.CustomChecks, Category.EndpointFailures),
                    Category = Category.CustomChecks,
                    RelatedTo = new List<string>() { string.Format("endpoint/{0}/{1}", message.Category, string.Empty) } //TODO: Pass in the machine name
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
