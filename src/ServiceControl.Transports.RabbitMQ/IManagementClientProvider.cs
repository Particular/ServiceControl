namespace ServiceControl.Transports.RabbitMQ;

using System;
using NServiceBus.Transport.RabbitMQ.ManagementApi;

interface IManagementClientProvider
{
    Lazy<ManagementClient> GetManagementClient();
}
