namespace ServiceControl.Operations
{
    using System.Linq;
    using Persistence;

    class MassTransitDetector(IConnectedApplicationsDataStore connectedApplicationsDataStore) : IEnrichImportedErrorMessages
    {
        public void Enrich(ErrorEnricherContext context)
        {
            if (alreadyDetected)
            {
                return;
            }

            if (context.Headers.Any(h => IsMassTransitHeader(h.Key)))
            {
                alreadyDetected = true;

                _ = connectedApplicationsDataStore.Add("MassTransitConnector");
            }
        }

        static bool IsMassTransitHeader(string headerName) => headerName.StartsWith("MT-", System.StringComparison.InvariantCultureIgnoreCase);

        static bool alreadyDetected;
    }
}
