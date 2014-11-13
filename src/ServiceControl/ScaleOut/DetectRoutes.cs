namespace ServiceControl.MessageFailures.Api
{
    using System.Collections.Generic;
    using NServiceBus;
    using Raven.Client;
    using ServiceControl.Contracts.EndpointControl;

    public class DetectRoutes : IHandleMessages<EndpointStarted>
    {
        public IDocumentSession Session { get; set; }

        public void Handle(EndpointStarted message)
        {
            var parts = message.EndpointDetails.Name.Split('-');


            if (parts.Length != 2)
            {
                return;
            }

            var logicalEndpoint = parts[0];

            var enpointRoutes = Session.Load<EndpointRoutes>(logicalEndpoint) ?? new EndpointRoutes{Id = logicalEndpoint};

            //todo: we need to get the real address
            var address = message.EndpointDetails.Name + "@" + message.EndpointDetails.Host;

            if (enpointRoutes.Routes.Contains(address))
            {
                return;
            }

            enpointRoutes.Routes.Add(address);

            Session.Store(enpointRoutes);
        }
    }

    public class EndpointRoutes
    {
        public EndpointRoutes()
        {
            Routes = new List<string>();
        }
        public string Id { get; set; }
        public List<string> Routes { get; set; }
    }
}