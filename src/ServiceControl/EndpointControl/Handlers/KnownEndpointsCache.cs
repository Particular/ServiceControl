namespace ServiceControl.EndpointControl.Handlers
{
    using System;
    using System.Collections.Concurrent;
    using NServiceBus;

    public class KnownEndpointsCache : INeedInitialization
    {
        public bool TryAdd(Guid key)
        {
            // We are adding the key right away to prevent Raven Concurrency exceptions.
            // If we add after the message is processed, before the value could be added in the 
            // dictionary,  we try to process the same message again causing the concurrency exception. 
            return processed.TryAdd(key, new CachedEntry{TimeAdded = DateTime.UtcNow});
        }

        readonly ConcurrentDictionary<Guid, CachedEntry> processed = new ConcurrentDictionary<Guid, CachedEntry>();

        class CachedEntry
        {
            public bool Processed { get; set; }
            public DateTime TimeAdded { get; set; }
        }

        public void Customize(BusConfiguration configuration)
        {
            configuration.RegisterComponents(c => c.ConfigureComponent<KnownEndpointsCache>(DependencyLifecycle.SingleInstance));
        }
    }


}