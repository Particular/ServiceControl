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
            return processed.TryAdd(key, new CachedEntry(){TimeAdded = DateTime.UtcNow});
        }

        public void MarkAsProcessed(Guid key)
        {
            //processed.TryAdd(key, true);
            //TODO: Because we are adding to the dictionary rightaway, if the RegisterEndpoint message gets rolled back, we need to add a timer that checks and removes unprocessed items after a while -- coz message couldve rolled back.

        }

        public void Init()
        {
            Configure.Component<KnownEndpointsCache>(DependencyLifecycle.SingleInstance);
        }

        readonly ConcurrentDictionary<Guid, CachedEntry> processed = new ConcurrentDictionary<Guid, CachedEntry>();

        class CachedEntry
        {
            public bool Processed { get; set; }
            public DateTime TimeAdded { get; set; }
        }
    
    }


}