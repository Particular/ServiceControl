namespace ServiceBus.Management.EndpointControl
{
    using System.Linq;
    using Infrastructure.Extensions;
    using Infrastructure.Nancy.Modules;
    using Infrastructure.RavenDB.Indexes;
    using Nancy;
    using Raven.Client;

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
    }
}