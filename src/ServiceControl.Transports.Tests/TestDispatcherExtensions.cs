namespace ServiceControl.Transport.Tests
{
    using System;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus.Routing;
    using NServiceBus.Transport;
    using ServiceControl.Transports;

    static class TestDispatcherExtensions
    {
        public static Task SendTestMessage(this IMessageDispatcher dispatcher, string queue, string content, ITransportCustomization transportCustomization)
        {
            var transportOperation = new TransportOperation(
             new OutgoingMessage(Guid.NewGuid().ToString(), [],
                 Encoding.UTF8.GetBytes(content)),
             new UnicastAddressTag(transportCustomization.ToTransportQualifiedQueueName(queue)), []);

            return dispatcher.Dispatch(new TransportOperations(transportOperation), new TransportTransaction(), CancellationToken.None);
        }
    }
}