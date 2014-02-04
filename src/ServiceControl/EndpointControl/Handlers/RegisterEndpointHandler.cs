namespace ServiceControl.EndpointControl.Handlers
{
    using Contracts.EndpointControl;
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
            var machine = message.Endpoint.Machine;
            var endpointName = message.Endpoint.Name;

            //Injecting store in this class because we want to know about ConcurrencyExceptions do that EndpointsCache.MarkAsProcessed is not called if the save fails.

            using (var session = Store.OpenSession()) 
            {
                session.Advanced.UseOptimisticConcurrency = true;
                var id = "KnownEndpoints/" + endpointName;

                var knownEndpoint = session.Load<KnownEndpoint>(id) ?? new KnownEndpoint {Id = id};

                if (knownEndpoint.Name == null)
                {
                    //new endpoint
                    Bus.Publish(new NewEndpointDetected
                    {
                        Endpoint = endpointName,
                        Machine = machine,
                        DetectedAt = message.DetectedAt
                    });
                }

                knownEndpoint.Name = endpointName;

                if (!knownEndpoint.Machines.Contains(machine))
                {
                    //new machine found
                    knownEndpoint.Machines.Add(machine);

                    if (knownEndpoint.Machines.Count > 1)
                    {
                        Bus.Publish(new NewMachineDetectedForEndpoint
                        {
                            Endpoint = endpointName,
                            Machine = machine,
                            DetectedAt = message.DetectedAt
                        });
                    }
                }

                session.Store(knownEndpoint);
                session.SaveChanges();
            }

            EndpointsCache.MarkAsProcessed(endpointName + machine);
        }
    }
}