namespace ServiceControl.CustomChecks
{
    using System.Linq;
    using Infrastructure.Extensions;
    using Nancy;
    using Raven.Client;
    using Raven.Client.Linq;
    using ServiceBus.Management.Infrastructure.Extensions;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;

    public class CustomChecksModule : BaseModule
    {
        public CustomChecksModule()
        {
            Get["/customchecks"] = _ =>
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

            Delete["/customchecks/{id}"] = parameters =>
            {
                using (var session = Store.OpenSession())
                {
                    string id = parameters.id;

                    var customCheck = session.Load<CustomCheck>(string.Format("customchecks/{0}", id));

                    if (customCheck != null)
                    {
                        session.Delete(customCheck);
                        session.SaveChanges();
                    }
                }

                return HttpStatusCode.NoContent;
            };
        }
    }
}