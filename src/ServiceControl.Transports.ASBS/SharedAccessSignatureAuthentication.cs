namespace ServiceControl.Transports.ASBS
{
    using Azure.Messaging.ServiceBus.Administration;
    using NServiceBus;

    public class SharedAccessSignatureAuthentication : AuthenticationMethod
    {
        public SharedAccessSignatureAuthentication(string connectionString) => ConnectionString = connectionString;

        public string ConnectionString { get; }

        public override ServiceBusAdministrationClient BuildManagementClient()
            => new ServiceBusAdministrationClient(ConnectionString);

        public override void ConfigureConnection(TransportExtensions<AzureServiceBusTransport> transport)
        {
            transport.ConnectionString(ConnectionString);
        }
    }
}