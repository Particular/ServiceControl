namespace ServiceControl.Transports.RabbitMQ;

using NServiceBus.Transport.RabbitMQ.ManagementApi;

interface IManagementClientProvider
{
    ManagementClient ManagementClient { get; }
}
