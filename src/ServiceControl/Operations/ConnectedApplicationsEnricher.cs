namespace ServiceControl.Operations
{
    using System.Linq;
    using ServiceControl.Persistence;

    class ConnectedApplicationsEnricher(IConnectedApplicationsDataStore connectedApplicationsDataStore) : IEnrichImportedErrorMessages
    {
        static string MassTransitHeaderPrefix = "MT-";

        public void Enrich(ErrorEnricherContext context)
        {
            if (!alreadyAddedMassTransit && context.Headers.Any(incomingHeader => incomingHeader.Key.StartsWith(MassTransitHeaderPrefix, System.StringComparison.InvariantCultureIgnoreCase)))
            {
                alreadyAddedMassTransit = true;
                _ = connectedApplicationsDataStore.Add("MassTransitConnector");
            }
        }

        static bool alreadyAddedMassTransit = false;
    }
}
