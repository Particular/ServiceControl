namespace ServiceControl.EndpointControl.Handlers
{
    using System.Collections.Generic;
    using NServiceBus;

    internal class KnownEndpointsCache : INeedInitialization
    {
        public bool TryAdd(string key)
        {
            return !processed.ContainsKey(key);
        }

        public void MarkAsProcessed(string key)
        {
            processed[key] = true;
        }

        public void Init()
        {
            Configure.Component<KnownEndpointsCache>(DependencyLifecycle.SingleInstance);
        }

        readonly Dictionary<string, bool> processed = new Dictionary<string, bool>();
    }
}