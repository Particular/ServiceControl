namespace ServiceControl.CompositeViews.Messages
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Infrastructure.Extensions;
    using Nancy;
    using Raven.Client;
    using Raven.Client.Linq;

    public class MessagesByConversationApi : ScatterGatherApiMessageView<string>
    {
        public override async Task<QueryResult<List<MessagesView>>> LocalQuery(Request request, string input)
        {
            using (var session = Store.OpenAsyncSession())
            {
                var results = await session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                    .Statistics(out var stats)
                    .Where(m => m.ConversationId == input)
                    .Sort(request)
                    .Paging(request)
                    .TransformWith<MessagesViewTransformer, MessagesView>()
                    .ToListAsync()
                    .ConfigureAwait(false);

                return new QueryResult<List<MessagesView>>(results.ToList(), stats.ToQueryStatsInfo());
            }
        }
    }
}