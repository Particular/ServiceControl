namespace ServiceControl.CompositeViews.Messages
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Infrastructure.Extensions;
    using MessageFailures;
    using MessageFailures.Api;
    using Raven.Client.Documents;
    using ServiceBus.Management.Infrastructure.Settings;

    class SearchApi : ScatterGatherApiMessageView<string>
    {
        public SearchApi(IDocumentStore documentStore, Settings settings, Func<HttpClient> httpClientFactory) : base(documentStore, settings, httpClientFactory)
        {
        }

        protected override async Task<QueryResult<IList<MessagesView>>> LocalQuery(HttpRequestMessage request, string input)
        {
            using (var session = Store.OpenAsyncSession())
            {
                //TODO:RAVEN5 transformers are missing
                var results = await session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                    .Statistics(out var stats)
                    .Search(x => x.Query, input)
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