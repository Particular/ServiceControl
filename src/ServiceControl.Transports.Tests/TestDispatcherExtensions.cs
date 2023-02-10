﻿namespace ServiceControl.Transport.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using NServiceBus.Extensibility;
    using NServiceBus.Routing;
    using NServiceBus.Transport;

    static class TestDispatcherExtensions
    {
        public static Task SendTestMessage(this IDispatchMessages dispatcher, string queue, string content)
        {
            var transportOperation = new TransportOperation(
             new OutgoingMessage(Guid.NewGuid().ToString(), new Dictionary<string, string>(),
                 Encoding.UTF8.GetBytes(content)),
             new UnicastAddressTag(queue), DispatchConsistency.Default);

            return dispatcher.Dispatch(new TransportOperations(transportOperation), new TransportTransaction(), new ContextBag());
        }
    }
}