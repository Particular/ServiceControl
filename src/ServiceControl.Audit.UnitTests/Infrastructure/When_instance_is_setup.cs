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
    using NServiceBus.Raw;
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

    internal class FakeTransport : ITransportCustomization
    {
        public static string UserNameUsed;
        public static IList<string> QueuesCreated;

        public RawEndpointConfiguration CreateRawEndpointForReturnToSenderIngestion(string name,
            Func<MessageContext, IMessageDispatcher, CancellationToken, Task> onMessage,
            TransportSettings transportSettings) =>
            throw new NotImplementedException();

        public void CustomizeServiceControlEndpoint(EndpointConfiguration endpointConfiguration,
            TransportSettings transportSettings) => throw new NotImplementedException();

        public void CustomizeSendOnlyEndpoint(EndpointConfiguration endpointConfiguration,
            TransportSettings transportSettings) => throw new NotImplementedException();

        public void CustomizeMonitoringEndpoint(EndpointConfiguration endpointConfiguration,
            TransportSettings transportSettings) => throw new NotImplementedException();

        public IProvideQueueLength CreateQueueLengthProvider() => throw new NotImplementedException();

        public Task<IMessageDispatcher> InitializeDispatcher(string name, TransportSettings transportSettings) =>
            throw new NotImplementedException();

        public Task<IQueueIngestor> InitializeQueueIngestor(string queueName, TransportSettings transportSettings,
            Func<MessageContext, Task> onMessage, Func<ErrorContext, Task<ErrorHandleResult>> onError,
            Func<string, Exception, Task> onCriticalError) =>
            throw new NotImplementedException();

        public Task ProvisionQueues(string username, TransportSettings transportSettings,
            IEnumerable<string> additionalQueues)
        {
            UserNameUsed = username;
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