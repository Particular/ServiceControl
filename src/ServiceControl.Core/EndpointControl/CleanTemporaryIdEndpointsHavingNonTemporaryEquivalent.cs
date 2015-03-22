namespace ServiceControl.EndpointControl
{
    using System.Linq;
    using NServiceBus;
    using Raven.Client;
    using ServiceControl.CompositeViews.Endpoints;

    public class CleanTemporaryIdEndpointsHavingNonTemporaryEquivalent : IWantToRunWhenBusStartsAndStops
    {
        public IDocumentStore DocumentStore { get; set; }

        public void Start()
        {
            using (var session = DocumentStore.OpenSession())
            {
                var endpoints = session.Query<KnownEndpoint, KnownEndpointIndex>().ToList();

                foreach (var knownEndpoints in endpoints.GroupBy(e => e.EndpointDetails.Host + e.EndpointDetails.Name))
                {
                    var fixedIdsCount = knownEndpoints.Count(e => !e.HasTemporaryId);

                    //If we have knowEndpoints with non temp ids, we should delete all temp ids ones.
                    if (fixedIdsCount > 0)
                    {
                        foreach (var endpoint in knownEndpoints.Where(e => e.HasTemporaryId))
                        {
                            DocumentStore.DatabaseCommands.Delete(DocumentStore.Conventions.DefaultFindFullDocumentKeyFromNonStringIdentifier(endpoint.Id, typeof(KnownEndpoint), false), null);

                        }
                    }
                }
            }
        }

        public void Stop()
        {
        }
    }
}