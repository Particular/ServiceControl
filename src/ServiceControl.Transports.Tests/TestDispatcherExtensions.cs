namespace ServiceControl.Transport.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus.Routing;
    using NServiceBus.Transport;

    static class TestDispatcherExtensions
    {
        public static Task SendTestMessage(this IMessageDispatcher dispatcher, string queue, string content)
        {
            var transportOperation = new TransportOperation(
             new OutgoingMessage(Guid.NewGuid().ToString(), new Dictionary<string, string>(),
                 Encoding.UTF8.GetBytes(content)),
             new UnicastAddressTag(queue), new DispatchProperties());

            return dispatcher.Dispatch(new TransportOperations(transportOperation), new TransportTransaction(), CancellationToken.None);
        }
    }
}