namespace ServiceControl.CompositeViews.Messages
{
    using System;
    using System.Linq;
    using Infrastructure.Extensions;
    using Nancy;
    using Raven.Client;
    using Raven.Client.Linq;
    using ServiceBus.Management.Infrastructure.Extensions;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;

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

            Get["/messagebody"] = parameters =>
            {
                string ids = null;

                if ((bool)Request.Query.id.HasValue)
                {
                    ids = (string)Request.Query.id;
                }

                if (ids == null)
                {
                    return HttpStatusCode.BadRequest;
                }

                var idsList = ids.Replace(" ", String.Empty).Split(',');

                using (var session = Store.OpenSession())
                {
                    var messages =
                        session.Advanced.LuceneQuery<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                            .Where(string.Format("UniqueMessageId: ({0})", String.Join(" OR ", idsList)))
                            .SetResultTransformer(new MessagesBodyTransformer().TransformerName)
                            .SelectFields<MessagesBodyTransformer.Result>()
                            .ToArray();

                    return Negotiate.WithModel(messages);
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