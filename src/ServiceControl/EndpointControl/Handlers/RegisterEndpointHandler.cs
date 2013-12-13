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

                var knownEndpoint = session.Load<KnownEndpoint>(name) ??new KnownEndpoint{Id =name};

                if (knownEndpoint.Name == null)
                {
                    //new endpoint
                    Bus.Publish(new NewEndpointDetected
                    {
                        Endpoint = name
                    });
                }
                knownEndpoint.Name = name;


                var machine = message.Endpoint.Machine;

                if (!knownEndpoint.Machines.Contains(machine))
                {
                    //new machine found
                    knownEndpoint.Machines.Add(machine);

                    Bus.Publish(new NewMachineDetectedForEndpoint
                    {
                        Endpoint = name,
                        Machine = machine
                    });
                }

                session.Store(knownEndpoint);

                session.SaveChanges();
            }
        }
    }
}