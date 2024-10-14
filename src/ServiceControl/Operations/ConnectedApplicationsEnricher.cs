namespace ServiceControl.Operations
{
    using System.Collections.Concurrent;
    using Persistence;

    class ConnectedApplicationsEnricher(IConnectedApplicationsDataStore connectedApplicationsDataStore) : IEnrichImportedErrorMessages
    {
        public static string ConnectedAppHeaderName = "ServiceControl.ConnectedApplication.Id";

        public void Enrich(ErrorEnricherContext context)
        {
            var headers = context.Headers;

            if (headers.TryGetValue(ConnectedAppHeaderName, out var connectedApplicationName))
            {

                if (connectedApplicationsCache.TryAdd(connectedApplicationName, true))
                {
                    _ = connectedApplicationsDataStore.AddIfNotExists(connectedApplicationName);
                }
            }
        }

        ConcurrentDictionary<string, bool> connectedApplicationsCache = new();
    }
}
