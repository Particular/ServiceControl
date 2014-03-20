namespace ServiceControl.EndpointControl.Handlers
{
    using System;
    using System.Collections.Concurrent;
    using NServiceBus;

    public class KnownEndpointsCache : INeedInitialization
    {
        public bool TryAdd(Guid key)
        {
            return !processed.ContainsKey(key);
        }

        public void MarkAsProcessed(Guid key)
        {
            processed.TryAdd(key, true);
        }

        public void Init()
        {
            Configure.Component<KnownEndpointsCache>(DependencyLifecycle.SingleInstance);
        }

        readonly ConcurrentDictionary<Guid, bool> processed = new ConcurrentDictionary<Guid, bool>();
    }
}