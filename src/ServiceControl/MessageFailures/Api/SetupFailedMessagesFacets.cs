namespace ServiceControl.MessageFailures.Api
{
    using System.Collections.Generic;
    using NServiceBus;
    using Raven.Abstractions.Data;
    using Raven.Client;

    class SetupFailedMessagesFacets : IWantToRunWhenBusStartsAndStops
    {
        public IDocumentStore Store { get; set; }
        
        public void Start()
        {
            var facets = new List<Facet>
            {
                new Facet {Name = "Name", DisplayName="Endpoints"},
                new Facet {Name = "Machine", DisplayName = "Machines"},
                new Facet {Name = "MessageType", DisplayName = "Message types"},
            };

            using (var s = Store.OpenSession())
            {
                s.Store(new FacetSetup { Id = "facets/failedMessagesFacets", Facets = facets });
                s.SaveChanges();
            }
        }

        public void Stop()
        {
        }
    }
}
