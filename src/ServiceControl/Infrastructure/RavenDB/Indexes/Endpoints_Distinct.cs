namespace ServiceBus.Management.Infrastructure.RavenDB.Indexes
{
    using System.Linq;
    using Raven.Abstractions.Indexing;
    using Raven.Client.Indexes;
    using ServiceControl.Contracts.Operations;
    using ServiceControl.MessageAuditing;
    using ServiceControl.MessageFailures;

    public class Endpoints_Distinct : AbstractMultiMapIndexCreationTask<Endpoints_Distinct.Result>
    {
        public Endpoints_Distinct()
        {
            AddMap<ProcessedMessage>(messages => from message in messages
                select new
                {
                    Endpoint = message.SendingEndpoint
                });
            AddMap<ProcessedMessage>(messages => from message in messages
                select new
                {
                    Endpoint = message.ReceivingEndpoint
                });

            AddMap<FailedMessage>(messages => from message in messages
                                             select new
                                             {
                                                 Endpoint = message.ProcessingAttempts.Last().FailingEndpoint
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