namespace ServiceControl.EndpointControl.Handlers
{
    using System;
    using System.Collections.Concurrent;
    using NServiceBus;

    public class KnownEndpointsCache : INeedInitialization
    {
        static object marker = new object();

        public bool TryAdd(Guid key)
        {
            // We are adding the key right away to prevent Raven Concurrency exceptions.
            // If we add after the message is processed, before the value could be added in the 
            // dictionary,  we try to process the same message again causing the concurrency exception. 

            return processed.TryAdd(key, marker);
        }

        readonly ConcurrentDictionary<Guid, object> processed = new ConcurrentDictionary<Guid, object>();

        public void Customize(BusConfiguration configuration)
        {
            configuration.RegisterComponents(c => c.ConfigureComponent<KnownEndpointsCache>(DependencyLifecycle.SingleInstance));
        }
    }


}