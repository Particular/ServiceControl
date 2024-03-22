namespace ServiceControl.Audit.UnitTests.Infrastructure
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Audit.Infrastructure.Hosting;
    using Audit.Infrastructure.Hosting.Commands;
    using Audit.Infrastructure.Settings;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
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

            var setupCommand = new SetupCommand();
            await setupCommand.Execute(new HostArguments([]), settings);

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

        public void CustomizePrimaryEndpoint(EndpointConfiguration endpointConfiguration,
            TransportSettings transportSettings) => throw new NotImplementedException();

        public void CustomizeAuditEndpoint(EndpointConfiguration endpointConfiguration,
            TransportSettings transportSettings) => throw new NotImplementedException();

        public void CustomizeMonitoringEndpoint(EndpointConfiguration endpointConfiguration,
            TransportSettings transportSettings) => throw new NotImplementedException();

        public IProvideQueueLength CreateQueueLengthProvider() => throw new NotImplementedException();

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

        public Task<TransportInfrastructure> CreateTransportInfrastructure(string name, TransportSettings transportSettings, OnMessage onMessage = null,
            OnError onError = null, Func<string, Exception, Task> onCriticalError = null,
            TransportTransactionMode preferredTransactionMode = TransportTransactionMode.ReceiveOnly) =>
            throw new NotImplementedException();
    }

    class FakePersistenceConfiguration : IPersistenceConfiguration
    {
        public string Name => "FakePersister";

        public IEnumerable<string> ConfigurationKeys => [];

        public IPersistence Create(PersistenceSettings settings) => new FakePersistence();

        class FakePersistence : IPersistence
        {
            public void Configure(IServiceCollection services)
            {
            }

            public void ConfigureInstaller(IServiceCollection services)
            {
                services.AddHostedService<PersistenceInstaller>();
            }

            class PersistenceInstaller(IHostApplicationLifetime lifetime) : IHostedService
            {
                public Task StartAsync(CancellationToken cancellationToken)
                {
                    lifetime.StopApplication();
                    return Task.CompletedTask;
                }

                public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
            }
        }
    }
}