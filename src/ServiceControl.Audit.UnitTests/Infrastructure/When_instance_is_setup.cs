namespace ServiceControl.Audit.UnitTests.Infrastructure
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Audit.Infrastructure;
    using Audit.Infrastructure.Settings;
    using Microsoft.Extensions.DependencyInjection;
    using NServiceBus;
    using NServiceBus.Transport;
    using NUnit.Framework;
    using Persistence;
    using Transports;

    class When_instance_is_setup
    {
        [Test]
        public async Task Should_provision_queues()
        {
            var instanceInputQueueName = "SomeInstanceQueue";

            var settings = new Settings(instanceInputQueueName, typeof(FakeTransport).AssemblyQualifiedName, typeof(FakePersistenceConfiguration).AssemblyQualifiedName)
            {
                ForwardAuditMessages = true,
            };

            var setupBootstrapper = new SetupBootstrapper(settings);

            await setupBootstrapper.Run();

            CollectionAssert.AreEquivalent(new[]
            {
                instanceInputQueueName,
                $"{instanceInputQueueName}.Errors",
                settings.AuditQueue,
                settings.AuditLogQueue
            }, FakeTransport.QueuesCreated);
        }
    }

    class FakeTransport : ITransportCustomization
    {
        public static IList<string> QueuesCreated;
        public Task<TransportInfrastructure> CreateRawEndpointForReturnToSenderIngestion(string name, TransportSettings transportSettings, OnMessage onMessage, OnError onError, Func<string, Exception, Task> onCriticalError) => throw new NotImplementedException();

        public void CustomizeServiceControlEndpoint(EndpointConfiguration endpointConfiguration,
            TransportSettings transportSettings) => throw new NotImplementedException();

        public void CustomizeSendOnlyEndpoint(EndpointConfiguration endpointConfiguration,
            TransportSettings transportSettings) => throw new NotImplementedException();

        public void CustomizeMonitoringEndpoint(EndpointConfiguration endpointConfiguration,
            TransportSettings transportSettings) => throw new NotImplementedException();

        public IProvideQueueLength CreateQueueLengthProvider() => throw new NotImplementedException();

        public Task<IMessageDispatcher> InitializeDispatcher(string name, TransportSettings transportSettings) =>
            throw new NotImplementedException();

        public Task<TransportInfrastructure> CreateRawEndpointForIngestion(string queueName, TransportSettings transportSettings, OnMessage onMessage,
            OnError onError, Func<string, Exception, Task> onCriticalError) =>
            throw new NotImplementedException();

        public Task ProvisionQueues(TransportSettings transportSettings,
            IEnumerable<string> additionalQueues)
        {
            QueuesCreated = new List<string>(additionalQueues)
            {
                transportSettings.EndpointName,
                transportSettings.ErrorQueue
            };
            return Task.CompletedTask;
        }
    }

    class FakePersistenceConfiguration : IPersistenceConfiguration
    {
        public string Name => "FakePersister";

        public IEnumerable<string> ConfigurationKeys => new List<string>();

        public IPersistence Create(PersistenceSettings settings) => new FakePersistence();

        class FakePersistence : IPersistence
        {
            public IPersistenceLifecycle Configure(IServiceCollection serviceCollection) =>
                throw new NotImplementedException();
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