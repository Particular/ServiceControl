namespace ServiceControl.MessageFailures.Api
{
    using System.Collections.Generic;
    using Infrastructure.Extensions;
    using Nancy;
    using Raven.Abstractions.Data;
    using Raven.Client;
    using ServiceBus.Management.Infrastructure.Extensions;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;

    public class GetAllErrors : BaseModule
    {
        public GetAllErrors()
        {
            Head["/errors", true] = async (_, token) =>
            {
                using (var session = Store.OpenAsyncSession())
                {
                    var queryResult = await session.Advanced
                        .AsyncLuceneQuery<FailedMessageViewIndex.SortAndFilterOptions, FailedMessageViewIndex>()
                        .FilterByStatusWhere(Request)
                        .FilterByLastModifiedRange(Request)
                        .FilterByQueueAddress(Request)
                        .QueryResultAsync
                        .ConfigureAwait(false);

                    return Negotiate
                        .WithTotalCount(queryResult.TotalResults)
                        .WithEtagAndLastModified(queryResult.IndexEtag, queryResult.IndexTimestamp);
                }
            };

            Get["/errors", true] = async (_, token) =>
            {
                using (var session = Store.OpenAsyncSession())
                {
                    RavenQueryStatistics stats;

                    var results = await session.Advanced
                        .AsyncLuceneQuery<FailedMessageViewIndex.SortAndFilterOptions, FailedMessageViewIndex>()
                        .Statistics(out stats)
                        .FilterByStatusWhere(Request)
                        .FilterByLastModifiedRange(Request)
                        .FilterByQueueAddress(Request)
                        .Sort(Request)
                        .Paging(Request)
                        .SetResultTransformer(new FailedMessageViewTransformer().TransformerName)
                        .SelectFields<FailedMessageView>()
                        .ToListAsync();

                    return Negotiate
                        .WithModel(results)
                        .WithPagingLinksAndTotalCount(stats, Request)
                        .WithEtagAndLastModified(stats);
                }
            };

            Get["/endpoints/{name}/errors", true] = async (parameters, token) =>
            {
                using (var session = Store.OpenAsyncSession())
                {
                    string endpoint = parameters.name;

                    RavenQueryStatistics stats;
                    var results = await session.Advanced
                        .AsyncLuceneQuery<FailedMessageViewIndex.SortAndFilterOptions, FailedMessageViewIndex>()
                        .Statistics(out stats)
                        .FilterByStatusWhere(Request)
                        .AndAlso()
                        .WhereEquals("ReceivingEndpointName", endpoint)
                        .FilterByLastModifiedRange(Request)
                        .Sort(Request)
                        .Paging(Request)
                        .SetResultTransformer(new FailedMessageViewTransformer().TransformerName)
                        .SelectFields<FailedMessageView>()
                        .ToListAsync()
                        .ConfigureAwait(false);

                    return Negotiate
                        .WithModel(results)
                        .WithPagingLinksAndTotalCount(stats, Request)
                        .WithEtagAndLastModified(stats);
                }
            };

            Get["/errors/summary", true] = async (_, token) =>
            {
                using (var session = Store.OpenAsyncSession())
                {
                    var facetResults = await session.Query<FailedMessage, FailedMessageFacetsIndex>()
                        .ToFacetsAsync(new List<Facet>
                                    {
                                        new Facet {Name = "Name", DisplayName="Endpoints"},
                                        new Facet {Name = "Host", DisplayName = "Hosts"},
                                        new Facet {Name = "MessageType", DisplayName = "Message types"},
                                    })
                        .ConfigureAwait(false);

                    return Negotiate.WithModel(facetResults.Results);
                }
            };
        }
    }
}