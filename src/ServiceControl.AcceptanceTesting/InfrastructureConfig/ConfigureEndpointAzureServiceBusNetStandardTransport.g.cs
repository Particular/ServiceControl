namespace ServiceControl.AcceptanceTesting.InfrastructureConfig
{
    extern alias TransportASBS;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting.Support;
    using ServiceControl.AcceptanceTesting;
    using ServiceControl.Transports.ASBS;
    using ServiceControlInstaller.Engine.Instances;

    public class ConfigureEndpointAzureServiceBusNetStandardTransport : ITransportIntegration
    {
        public Task Configure(string endpointName, EndpointConfiguration configuration, RunSettings settings, PublisherMetadata publisherMetadata)
        {
            configuration.UseSerialization<NewtonsoftJsonSerializer>();

            // Relies on extern alias at the top of the file to disambiguate AzureServiceBusTransport
            // from NServiceBus.Azure.Transports.WindowsAzureServiceBus. If we need to add extension method calls,
            // include:
            //      using TransportASBS::NServiceBus;
            // in the using statements at the top.
            var transportConfig = configuration.UseTransport<TransportASBS::NServiceBus.AzureServiceBusTransport>();
            transportConfig.ConfigureNameShorteners();

            transportConfig.ConnectionString(ConnectionString);

            return Task.FromResult(0);
        }

        public Task Cleanup()
        {
            return Task.FromResult(0);
        }

        public string Name => TransportNames.AzureServiceBus;
        public string TypeName => $"{typeof(ServiceControl.Transports.ASBS.ASBSTransportCustomization).AssemblyQualifiedName}";
        public string ConnectionString { get; set; }
        public string ScrubPlatformConnection(string input) => input;
    }
}