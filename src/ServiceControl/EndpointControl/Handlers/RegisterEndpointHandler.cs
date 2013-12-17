namespace ServiceControl.EndpointControl.Handlers
{
    using Contracts.EndpointControl;
    using InternalMessages;
    using NServiceBus;
    using Raven.Client;

    public class RegisterEndpointHandler : IHandleMessages<RegisterEndpoint>
    {
        public IDocumentStore Store { get; set; }

        public IBus Bus { get; set; }

        public void Handle(RegisterEndpoint message)
        {
            using (var session = Store.OpenSession())
            {
                session.Advanced.UseOptimisticConcurrency = true;

                var name = message.Endpoint.Name;

                var knownEndpoint = session.Load<KnownEndpoint>(name) ?? new KnownEndpoint { Id = name };

                var machine = message.Endpoint.Machine;

                if (knownEndpoint.Name == null)
                {
                    //new endpoint
                    Bus.Publish(new NewEndpointDetected
                    {
                        Endpoint = name,
                        Machine = machine,
                        DetectedAt = message.DetectedAt
                    });
                }
                knownEndpoint.Name = name;


                if (!knownEndpoint.Machines.Contains(machine))
                {
                    //new machine found
                    knownEndpoint.Machines.Add(machine);

                    if (knownEndpoint.Machines.Count > 1)
                    {
                        Bus.Publish(new NewMachineDetectedForEndpoint
                        {
                            Endpoint = name,
                            Machine = machine,
                            DetectedAt = message.DetectedAt
                        });
                    }
                }

                session.Store(knownEndpoint);

                session.SaveChanges();
            }
        }
    }
}