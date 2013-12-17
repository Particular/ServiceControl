namespace ServiceControl.EndpointControl.Handlers
{
    using System.Collections.Concurrent;
    using Contracts.Operations;
    using NServiceBus;

    class KnownEndpointsCache:INeedInitialization
    {
        public ConcurrentDictionary<string, EndpointDetails> Endpoints 
        {
            get
            {
                return cache;
            } 
        }

        readonly ConcurrentDictionary<string, EndpointDetails> cache = new ConcurrentDictionary<string, EndpointDetails>();

        public void Init()
        {
            Configure.Component<KnownEndpointsCache>(DependencyLifecycle.SingleInstance);
        }
    }
}