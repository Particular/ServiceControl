namespace Particular.Backend.Debugging.RavenDB.Api
{
    using System.Linq;
    using Nancy;
    using Particular.Backend.Debugging.Api;
    using Raven.Client;
    using Raven.Client.Linq;
    using ServiceBus.Management.Infrastructure.Extensions;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;
    using ServiceControl.Infrastructure.Extensions;

    public class GetMessages : BaseModule
    {
        public GetMessages()
        {
            Get["/messages"] = parameters =>
            {
                using (var session = Store.OpenSession())
                {
                    RavenQueryStatistics stats;
                    var results = session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                        .IncludeSystemMessagesWhere(Request)
                        .Statistics(out stats)
                        .Sort(Request)
                        .Paging(Request)
                        .TransformWith<MessagesViewTransformer, MessagesView>()
                        .ToArray();

                    return Negotiate.WithModel(results)
                                    .WithPagingLinksAndTotalCount(stats, Request)
                                    .WithEtagAndLastModified(stats);
                }
            };

            Get["/endpoints/{name}/messages"] = parameters =>
            {
                using (var session = Store.OpenSession())
                {
                    string endpoint = parameters.name;

                    RavenQueryStatistics stats;
                    var results = session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                        .IncludeSystemMessagesWhere(Request)
                        .Where(m => m.ReceivingEndpointName == endpoint)
                        .Statistics(out stats)
                        .Sort(Request)
                        .Paging(Request)
                        .TransformWith<MessagesViewTransformer, MessagesView>()
                        .ToArray();

                    return Negotiate
                        .WithModel(results)
                        .WithPagingLinksAndTotalCount(stats, Request)
                        .WithEtagAndLastModified(stats);
                }
            };
        }


    }
}