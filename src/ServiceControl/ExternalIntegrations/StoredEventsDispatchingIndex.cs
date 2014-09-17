namespace ServiceControl.ExternalIntegrations
{
    using System.Linq;
    using Raven.Client.Indexes;

    public class StoredEventsDispatchingIndex : AbstractIndexCreationTask<StoredEvent>
    {
        public StoredEventsDispatchingIndex()
        {
            Map = events => from c in events
                select new
                {
                    c.Dispatched,
                    c.RegistrationDate
                };
        }
    }
}