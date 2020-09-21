namespace ServiceControl.CompositeViews.Messages
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Infrastructure.Extensions;
    using MessageFailures;
    using Raven.Client.Documents;
    using ServiceBus.Management.Infrastructure.Settings;

    class GetAllMessagesForEndpointApi : ScatterGatherApiMessageView<string>
    {
        public GetAllMessagesForEndpointApi(IDocumentStore documentStore, Settings settings, Func<HttpClient> httpClientFactory) : base(documentStore, settings, httpClientFactory)
        {
        }

        protected override async Task<QueryResult<IList<MessagesView>>> LocalQuery(HttpRequestMessage request, string input)
        {
            using (var session = Store.OpenAsyncSession())
            {
                var results = await session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                    .IncludeSystemMessagesWhere(request)
                    .Where(m => m.ReceivingEndpointName == input, false)
                    .Statistics(out var stats)
                    .Sort(request)
                    .Paging(request)
                    .As<FailedMessage>()
                    .ToListAsync()
                    .ConfigureAwait(false);

                return new QueryResult<IList<MessagesView>>(results.ToMessagesView().ToList(), stats.ToQueryStatsInfo());
            }
        }
    }
}