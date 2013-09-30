namespace ServiceControl.Infrastructure.RavenDB.Facets
{
    using System.Collections.Generic;
    using NServiceBus;
    using Raven.Abstractions.Indexing;
    using Raven.Client;
    using Raven.Abstractions.Data;
    
    //TODO: This shouldn't be done every time the endpoint starts, but only at install time.
    public class MessageFailures : IWantToRunWhenBusStartsAndStops
    {
        public IDocumentStore DocumentStore { get; set; }
        public void Start()
        {
            // Create the index
            DocumentStore.DatabaseCommands.PutIndex("MessageFailures",
                   new IndexDefinition
                   {
                       Map = @"from message in docs 
                                where message.Status == ""Failed""
                                select new 
                                { 
                                    message.ReceivingEndpoint.Name, 
                                    message.ReceivingEndpoint.Machine, 
                                    message.MessageType,
                                    message.FailureDetails.Exception.ExceptionType,
                                    message.FailureDetails.Exception.Message,
                                    message.TimeSent
                                }"
                   }, true);


            // Create the facets for MessageFailures to facilitate easy searching.
            var facets = new List<Facet>
            {
                new Facet {Name = "Name", DisplayName="Endpoints"},
                new Facet {Name = "Machine", DisplayName = "Machines"},
                new Facet {Name = "MessageType", DisplayName = "Message Types"},
                //new Facet() {Name = "Custom Tags"}
            };

            using (var s = DocumentStore.OpenSession())
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


