namespace ServiceControl.CustomChecks
{
    using System.Linq;
    using Nancy;
    using Raven.Client;
    using Raven.Client.Linq;
    using ServiceBus.Management.Infrastructure.Extensions;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;

    public class CustomChecksModule : BaseModule
    {
        public CustomChecksModule()
        {
            Get["/customchecks"] = parameters =>
            {
                using (var session = Store.OpenSession())
                {
                    RavenQueryStatistics stats;
                    var results =
                        session.Query<CustomCheck>()
                            .Statistics(out stats)
                            .Where(c => c.Status == Status.Fail)
                            .Paging(Request)
                            .ToArray();

                    return Negotiate
                        .WithModel(results)
                        .WithPagingLinksAndTotalCount(stats, Request)
                        .WithEtagAndLastModified(stats);
                }
            };
        }

        public IDocumentStore Store { get; set; }
    }
}