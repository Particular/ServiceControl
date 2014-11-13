namespace ServiceControl.CustomChecks
{
    using System;
    using System.Linq;
    using Infrastructure.Extensions;
    using Nancy;
    using NServiceBus;
    using Raven.Client;
    using Raven.Client.Linq;
    using ServiceBus.Management.Infrastructure.Extensions;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;

    public class CustomChecksModule : BaseModule
    {
        public IBus Bus { get; set; }

        public CustomChecksModule()
        {
            Get["/customchecks"] = _ =>
            {
                using (var session = Store.OpenSession())
                {
                    RavenQueryStatistics stats;
                    var query =
                        session.Query<CustomCheck, CustomChecksIndex>().Statistics(out stats);

                    query = AddStatusFilter(query);

                    var results = query
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
                Guid id = parameters.id;

                Bus.SendLocal(new DeleteCustomCheck{Id = id});

                return HttpStatusCode.Accepted;
            };
        }

        IRavenQueryable<CustomCheck> AddStatusFilter(IRavenQueryable<CustomCheck> query)
        {
            string status = null;

            if ((bool) Request.Query.status.HasValue)
            {
                status = (string) Request.Query.status;
            }

            if (status == null)
            {
                return query;
            }

            if (status == "fail")
            {
                query = query.Where(c => c.Status == Status.Fail);
            }

            if (status == "pass")
            {
                query = query.Where(c => c.Status == Status.Pass);
            }
            return query;
        }
    }
}