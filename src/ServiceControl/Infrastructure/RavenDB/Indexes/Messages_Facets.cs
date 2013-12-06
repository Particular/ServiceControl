namespace ServiceControl.Infrastructure.RavenDB.Indexes
{
    using System.Collections.Generic;
    using NServiceBus;
    using Raven.Abstractions.Data;
    using Raven.Client;

    class Messages_Facets : IWantToRunWhenBusStartsAndStops
    {
        public IDocumentStore Store { get; set; }
        
        public void Start()
        {
            // Create the facets for MessageFailures to facilitate easy searching.
            var facets = new List<Facet>
            {
                new Facet {Name = "Name", DisplayName="Endpoints"},
                new Facet {Name = "Machine", DisplayName = "Machines"},
                new Facet {Name = "MessageType", DisplayName = "AuditMessage Types"},
            };

            using (var s = Store.OpenSession())
            {
                s.Store(new FacetSetup { Id = "facets/messageFailureFacets", Facets = facets });
                s.SaveChanges();
            }
        }

        public void Stop()
        {
        }
    }
}
