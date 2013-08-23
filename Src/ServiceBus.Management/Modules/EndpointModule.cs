namespace ServiceBus.Management.Modules
{
    using System.Linq;
    using Extensions;
    using global::Nancy;
    using Raven.Client;
    using RavenDB.Indexes;

    public class EndpointModule : BaseModule
    {
        public EndpointModule()
        {
            Get["/endpoints"] = parameters =>
            {
                using (var session = Store.OpenSession())
                {
                    RavenQueryStatistics stats;

                    var endpoints = session.Query<Endpoints_Distinct.Result, Endpoints_Distinct>()
                        .Statistics(out stats)
                        .Select(r => r.Endpoint)
                        .ToArray();

                    return Negotiate.WithModel(endpoints)
                        .WithEtagAndLastModified(stats);
                }
            };
        }

        public IDocumentStore Store { get; set; }
    }
}