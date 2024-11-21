namespace ServiceControl.Persistence
{
    using System.Linq;
    using Raven.Client.Documents.Indexes;

    class ConnectedApplicationIndex : AbstractIndexCreationTask<ConnectedApplication>
    {
        public ConnectedApplicationIndex()
        {
            Map = applications =>

                from application in applications
                select new
                {
                    application.Name,
                    application.SupportsHeartbeats
                };
        }
    }
}