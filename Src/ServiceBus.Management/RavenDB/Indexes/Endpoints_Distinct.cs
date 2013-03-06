namespace ServiceBus.Management.RavenDB.Indexes
{
    using System.Linq;
    using Raven.Client.Indexes;

    public class Endpoints_Distinct : AbstractIndexCreationTask<Message, Endpoints_Distinct.Result>
    {
        public class Result
        {
            public EndpointDetails Endpoint { get; set; }
        }

        public Endpoints_Distinct()
        {
            Map = messages => from message in messages
                              select new
                                  {
                                      Endpoint = message.OriginatingEndpoint
                                  };

            Reduce = results => from result in results
                                group result by result.Endpoint
                                into g
                                select new
                                    {
                                        Endpoint = g.Key
                                    };
        }
    }
}