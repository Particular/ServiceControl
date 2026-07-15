namespace ServiceControl.Transports.ASBS
{
    using Azure.Messaging.ServiceBus.Administration;
    using NServiceBus;

    public class SharedAccessSignatureAuthentication : AuthenticationMethod
    {
        public SharedAccessSignatureAuthentication(string connectionString) => ConnectionString = connectionString;

        public string ConnectionString { get; }

        public override ServiceBusAdministrationClient BuildManagementClient(ServiceBusAdministrationClientOptions options = null)
            => options is null
                ? new ServiceBusAdministrationClient(ConnectionString)
                : new ServiceBusAdministrationClient(ConnectionString, options);

        public override AzureServiceBusTransport CreateTransportDefinition(ConnectionSettings connectionSettings, TopicTopology topology)
            => new AzureServiceBusTransport(ConnectionString, topology);
    }
}