namespace ServiceControl.Audit.UnitTests.Infrastructure
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Runtime.Loader;
    using System.Threading.Tasks;
    using Audit.Infrastructure.Hosting;
    using Audit.Infrastructure.Hosting.Commands;
    using Audit.Infrastructure.Settings;
    using Microsoft.Extensions.DependencyInjection;
    using NServiceBus;
    using NServiceBus.Transport;
    using NUnit.Framework;
    using Transports;

    class When_instance_is_setup
    {
        [Test]
        public async Task Should_provision_queues()
        {
            var manifest = new TransportManifest
            {
                Definitions = [new TransportManifestDefinition
                {
                    Name = "FakeTransport",
                    Location = AppContext.BaseDirectory,
                    AssemblyName = Assembly.GetExecutingAssembly().GetName().Name,
                    TypeName = typeof(FakeTransport).AssemblyQualifiedName
                }]
            };

            TransportManifestLibrary.TransportManifests.Add(manifest);

            var instanceInputQueueName = "SomeInstanceQueue";

            var settings = new Settings("FakeTransport", "InMemory")
            {
                InstanceName = instanceInputQueueName,
                ForwardAuditMessages = true,
                AssemblyLoadContextResolver = static _ => AssemblyLoadContext.Default
            };

            var setupCommand = new SetupCommand();
            await setupCommand.Execute(new HostArguments([]), settings);

            Assert.That(FakeTransport.QueuesCreated, Is.EquivalentTo(new[]
            {
                instanceInputQueueName,
                $"{instanceInputQueueName}.Errors",
                settings.AuditQueue,
                settings.AuditLogQueue
            }));
        }
    }

    class FakeTransport : ITransportCustomization
    {
        public static IList<string> QueuesCreated;

        public void CustomizePrimaryEndpoint(EndpointConfiguration endpointConfiguration,
            TransportSettings transportSettings) => throw new NotImplementedException();

        public void AddTransportForPrimary(IServiceCollection services, TransportSettings transportSettings) => throw new NotImplementedException();

        public void CustomizeAuditEndpoint(EndpointConfiguration endpointConfiguration,
            TransportSettings transportSettings) => throw new NotImplementedException();

        public void AddTransportForAudit(IServiceCollection services, TransportSettings transportSettings) => throw new NotImplementedException();

        public void CustomizeMonitoringEndpoint(EndpointConfiguration endpointConfiguration,
            TransportSettings transportSettings) => throw new NotImplementedException();

        public void AddTransportForMonitoring(IServiceCollection services, TransportSettings transportSettings) => throw new NotImplementedException();

        public Task ProvisionQueues(TransportSettings transportSettings, IEnumerable<string> additionalQueues)
        {
            QueuesCreated =
            [
                .. additionalQueues,
                transportSettings.EndpointName,
                transportSettings.ErrorQueue
            ];
            return Task.CompletedTask;
        }

        public Task<TransportInfrastructure> CreateTransportInfrastructure(string name, TransportSettings transportSettings,
            OnMessage onMessage = null, OnError onError = null, Func<string, Exception, Task> onCriticalError = null,
            TransportTransactionMode preferredTransactionMode = TransportTransactionMode.ReceiveOnly) =>
            throw new NotImplementedException();
        public string ToTransportQualifiedQueueName(string queueName) => queueName;
    }
}