namespace ServiceControl.Audit.UnitTests.Infrastructure
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using NServiceBus;
    using NServiceBus.Raw;
    using NUnit.Framework;
    using ServiceControl.Audit.Infrastructure;
    using ServiceControl.Audit.Infrastructure.Settings;
    using ServiceControl.Audit.Persistence;
    using ServiceControl.Transports;

    class When_instance_is_setup
    {
        [Test]
        public async Task Should_provision_queues()
        {
            var instanceInputQueueName = "SomeInstanceQueue";
            var userName = "SomeUser";

            var settings = new Settings(instanceInputQueueName, typeof(FakeTransport).AssemblyQualifiedName, typeof(FakePersistenceConfiguration).AssemblyQualifiedName)
            {
                ForwardAuditMessages = true,
            };

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

    class FakePersistenceConfiguration : IPersistenceConfiguration
    {
        public string Name => "FakePersister";

        public IEnumerable<string> ConfigurationKeys => new List<string>();

        public IPersistence Create(PersistenceSettings settings) => new FakePersistence();

        class FakePersistence : IPersistence
        {
            public IPersistenceLifecycle Configure(IServiceCollection serviceCollection) => throw new System.NotImplementedException();
            public IPersistenceInstaller CreateInstaller() => new FakePersistenceInstaller();

            class FakePersistenceInstaller : IPersistenceInstaller
            {
                public Task Install(CancellationToken cancellationToken = default)
                {
                    return Task.CompletedTask;
                }
            }
        }
    }
}