namespace ServiceControl.Transports.ASBS
{
    using Azure.Messaging.ServiceBus.Administration;
    using NServiceBus;

    public abstract class AuthenticationMethod
    {
        public abstract ServiceBusAdministrationClient BuildManagementClient();
        public abstract AzureServiceBusTransport CreateTransportDefinition(ConnectionSettings connectionSettings, TopicTopology topology);
    }
}