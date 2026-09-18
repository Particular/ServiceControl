namespace ServiceControl.Auditing
{
    using System.Threading;
    using System.Threading.Tasks;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Connection;

    // /api/connection is what ServicePulse and the Platform Connector plugin read to configure
    // endpoints. Without an audit remote it stops advertising where audit and saga data should go, so a
    // primary holding audit data locally supplies the same shapes the audit instance supplies.
    class AuditPlatformConnectionDetailsProvider(Settings settings) : IProvidePlatformConnectionDetails
    {
        public Task ProvideConnectionDetails(PlatformConnectionDetails connection, CancellationToken cancellationToken = default)
        {
            connection.Add("MessageAudit", new MessageAuditConnectionDetails
            {
                Enabled = true,
                AuditQueue = settings.AuditQueue
            });

            connection.Add("SagaAudit", new SagaAuditConnectionDetails
            {
                Enabled = true,
                SagaAuditQueue = settings.AuditQueue
            });

            return Task.CompletedTask;
        }

        // HINT: These should match the types in the PlatformConnector package
        public class MessageAuditConnectionDetails
        {
            public bool Enabled { get; set; }
            public string AuditQueue { get; set; }
        }

        public class SagaAuditConnectionDetails
        {
            public bool Enabled { get; set; }
            public string SagaAuditQueue { get; set; }
        }
    }
}
