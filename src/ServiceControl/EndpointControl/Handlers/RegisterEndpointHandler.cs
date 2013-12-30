namespace ServiceControl.EndpointControl.Handlers
{
    using Contracts.EndpointControl;
    using InternalMessages;
    using NServiceBus;
    using Raven.Client;

    public class RegisterEndpointHandler : IHandleMessages<RegisterEndpoint>
    {
        public IDocumentSession Session { get; set; }

        public IBus Bus { get; set; }

        public void Handle(RegisterEndpoint message)
        {
            var name = message.Endpoint.Name;

            var knownEndpoint = Session.Load<KnownEndpoint>(name) ?? new KnownEndpoint { Id = name };

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

            Session.Store(knownEndpoint);
        }
    }
}