namespace ServiceControl.Audit.AcceptanceTests.Auditing
{
    using System.Collections.Generic;
    using System.Configuration;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Raw;
    using NUnit.Framework;
    using ServiceControl.Audit.Infrastructure;
    using ServiceControl.Audit.Infrastructure.Settings;
    using ServiceControl.Transports;

    class When_instance_is_setup : AcceptanceTest
    {
        [Test]
        public async Task Should_provision_queues()
        {
            ConfigurationManager.AppSettings.Set("ServiceControl.Audit/TransportType", typeof(FakeTransport).AssemblyQualifiedName);
            ConfigurationManager.AppSettings.Set("ServiceControl.Audit/PersistenceType", StorageConfiguration.PersistenceType);

            var instanceInputQueueName = "SomeInstanceQueue";
            var userName = "SomeUser";

            var settings = new Settings(instanceInputQueueName)
            {
                ForwardAuditMessages = true,
            };

            settings.AuditLogQueue = $"{settings.AuditQueue}.log";

            var setupBootstrapper = new SetupBootstrapper(settings);

            await setupBootstrapper.Run(userName);

            Assert.AreEqual(userName, FakeTransport.UserNameUsed);
            CollectionAssert.AreEquivalent(new[]
            {
                instanceInputQueueName,
                $"{instanceInputQueueName}.Errors",
                settings.AuditQueue,
                settings.AuditLogQueue
            }, FakeTransport.QueuesCreated);
        }
    }

    class FakeTransport : TransportCustomization
    {
        public static string UserNameUsed;
        public static IList<string> QueuesCreated;

        public override Task ProvisionQueues(string username, TransportSettings transportSettings, IEnumerable<string> additionalQueues)
        {
            UserNameUsed = username;
            QueuesCreated = new List<string>(additionalQueues)
            {
                transportSettings.EndpointName,
                transportSettings.ErrorQueue
            };
            return Task.CompletedTask;
        }

        public override IProvideQueueLength CreateQueueLengthProvider() => throw new System.NotImplementedException();
        public override void CustomizeForReturnToSenderIngestion(RawEndpointConfiguration endpointConfiguration, TransportSettings transportSettings) => throw new System.NotImplementedException();
        public override void CustomizeMonitoringEndpoint(EndpointConfiguration endpointConfiguration, TransportSettings transportSettings) => throw new System.NotImplementedException();
        public override void CustomizeSendOnlyEndpoint(EndpointConfiguration endpointConfiguration, TransportSettings transportSettings) => throw new System.NotImplementedException();
        public override void CustomizeServiceControlEndpoint(EndpointConfiguration endpointConfiguration, TransportSettings transportSettings) => throw new System.NotImplementedException();
        protected override void CustomizeForQueueIngestion(RawEndpointConfiguration endpointConfiguration, TransportSettings transportSettings) => throw new System.NotImplementedException();
        protected override void CustomizeRawSendOnlyEndpoint(RawEndpointConfiguration endpointConfiguration, TransportSettings transportSettings) => throw new System.NotImplementedException();
    }
}