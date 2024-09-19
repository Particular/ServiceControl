namespace ServiceControl.Operations
{
    using System.Collections.Generic;
    using System.Linq;
    using ServiceBus.Management.Infrastructure.Settings;

    class ConnectedApplicationsEnricher(Settings settings) : IEnrichImportedErrorMessages
    {
        static List<string> MassTransitHeaders =
        [
            "MassTransitHeader1",
            "MassTransitHeader2",
            "MassTransitHeader3"
        ];

        public void Enrich(ErrorEnricherContext context)
        {
            if (!alreadyAddedMassTransit && context.Headers.Any(incomingHeader => MassTransitHeaders.Contains(incomingHeader.Key)))
            {
                alreadyAddedMassTransit = true;
                settings.ConnectedApplications.Add("MassTransitConnector");
            }
        }

        static bool alreadyAddedMassTransit = false;
    }
}
