namespace ServiceControl.CompositeViews.Endpoints
{
    using System.Linq;
    using Nancy;
    using Raven.Client;
    using ServiceBus.Management.Infrastructure.Extensions;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;

    public class GetEndpoints : BaseModule
    {
        public GetEndpoints()
        {
            Get["/endpoints"] = parameters =>
            {
                using (var session = Store.OpenSession())
                {
                    RavenQueryStatistics stats;

                    var endpoints = session.Query<EndpointsView, EndpointsViewIndex>()
                        .Statistics(out stats)
                        .ToArray();

                    return Negotiate.WithModel(endpoints)
                        .WithEtagAndLastModified(stats);
                }
            };
        }


    }
}