namespace ServiceBus.Management.Infrastructure.RavenDB.Indexes
{
    using System.Linq;
    using Raven.Abstractions.Indexing;
    using Raven.Client.Indexes;
    using ServiceControl.Contracts.Operations;
    using ServiceControl.MessageAuditing;

    public class Endpoints_Distinct : AbstractMultiMapIndexCreationTask<Endpoints_Distinct.Result>
    {
        public Endpoints_Distinct()
        {
            AddMap<AuditMessage>(messages => from message in messages
                select new
                {
                    Endpoint = message.OriginatingEndpoint
                });
            AddMap<AuditMessage>(messages => from message in messages
                select new
                {
                    Endpoint = message.ReceivingEndpoint
                });

            Reduce = results => from result in results
                group result by result.Endpoint
                into g
                select new
                {
                    Endpoint = g.Key
                };

            Store(x => x.Endpoint, FieldStorage.Yes);
        }

        public class Result
        {
            public EndpointDetails Endpoint { get; set; }
        }
    }
}