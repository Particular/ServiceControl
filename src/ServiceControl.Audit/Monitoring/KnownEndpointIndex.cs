namespace ServiceControl.CompositeViews.Endpoints
{
    using System.Linq;
    using EndpointControl;
    using Raven.Client.Indexes;

    class KnownEndpointIndex : AbstractIndexCreationTask<KnownEndpoint>
    {
        public KnownEndpointIndex()
        {
            Map = messages => from message in messages
                select new
                {
                   message.Id
                };

            DisableInMemoryIndexing = true;
        }
    }
}