namespace ServiceControl.EndpointControl.Handlers
{
    using System.Collections.Concurrent;
    using Contracts.Operations;
    using NServiceBus;

    internal class KnownEndpointsCache : INeedInitialization
    {
        public ConcurrentDictionary<string, EndpointDetails> Endpoints
        {
            get { return cache; }
        }

        public void Init()
        {
            Configure.Component<KnownEndpointsCache>(DependencyLifecycle.SingleInstance);
        }

        readonly ConcurrentDictionary<string, EndpointDetails> cache =
            new ConcurrentDictionary<string, EndpointDetails>();
    }
}