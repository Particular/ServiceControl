namespace ServiceControl.Transport.Tests;

using NServiceBus;
using ServiceControl.Transports.RabbitMQ;
using ServiceControl.Transports;

// Needed to call the protected methods of the TransportCustomization<T> class
class ExtendedClassToMakeProtectedMethodsPublic : RabbitMQClassicConventionalRoutingTransportCustomization
{
    public RabbitMQTransport PublicCreateTrasport(TransportSettings transportSettings)
    {
        return CreateTransport(transportSettings);
    }
}