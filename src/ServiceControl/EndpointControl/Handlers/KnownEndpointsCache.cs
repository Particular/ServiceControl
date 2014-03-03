namespace ServiceControl.EndpointControl.Handlers
{
    using System.Collections.Concurrent;
    using NServiceBus;

    internal class KnownEndpointsCache : INeedInitialization
    {
        public bool TryAdd(string key)
        {
            return !processed.ContainsKey(key);
        }

        public void MarkAsProcessed(string key)
        {
            processed.TryAdd(key, true);
        }

        public void Init()
        {
            Configure.Component<KnownEndpointsCache>(DependencyLifecycle.SingleInstance);
        }

        readonly ConcurrentDictionary<string, bool> processed = new ConcurrentDictionary<string, bool>();
    }
}