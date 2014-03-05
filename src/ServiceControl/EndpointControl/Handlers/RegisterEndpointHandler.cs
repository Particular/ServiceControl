namespace ServiceControl.EndpointControl.Handlers
{
    using Contracts.EndpointControl;
    using Infrastructure;
    using InternalMessages;
    using NServiceBus;
    using Raven.Client;

    internal class RegisterEndpointHandler : IHandleMessages<RegisterEndpoint>
    {
        public IDocumentStore Store { get; set; }
        public KnownEndpointsCache EndpointsCache { get; set; }
        public IBus Bus { get; set; }

        public void Handle(RegisterEndpoint message)
        {
            var machine = message.Endpoint.Host;
            var endpointName = message.Endpoint.Name;
            var id = DeterministicGuid.MakeId(endpointName, machine);

            //Injecting store in this class because we want to know about ConcurrencyExceptions so that EndpointsCache.MarkAsProcessed is not called if the save fails.

            using (var session = Store.OpenSession()) 
            {
                session.Advanced.UseOptimisticConcurrency = true;

                var knownEndpoint = session.Load<KnownEndpoint>(id);

                if (knownEndpoint == null)
                {
                    //new endpoint
                    Bus.Publish(new NewEndpointDetected
                    {
                        Endpoint = message.Endpoint,
                        DetectedAt = message.DetectedAt
                    });

                    knownEndpoint = new KnownEndpoint
                    {
                        Id = id,
                        Name = endpointName, 
                        HostDisplayName = machine,
                        HostId = message.Endpoint.HostId
                    };

                    session.Store(knownEndpoint);
                    session.SaveChanges();
                }
            }

            EndpointsCache.MarkAsProcessed(endpointName + machine);
        }
    }
}