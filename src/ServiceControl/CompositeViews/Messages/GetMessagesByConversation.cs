namespace ServiceControl.CompositeViews.Messages
{
    using System.Collections.Generic;
    using Raven.Client;
    using Raven.Client.Linq;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;
    using ServiceControl.Infrastructure.Extensions;

    public class GetMessagesByConversation : BaseModule
    {
        public GetMessagesByConversation()
        {
            Get["/conversations/{conversationid}", true] = async (parameters, token) =>
            {
                RavenQueryStatistics stats;
                IList<MessagesView> results;

                using (var session = Store.OpenAsyncSession())
                {
                    string conversationId = parameters.conversationid;

                    results = await session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                        .Statistics(out stats)
                        .Where(m => m.ConversationId == conversationId)
                        .Sort(Request)
                        .Paging(Request)
                        .TransformWith<MessagesViewTransformer, MessagesView>()
                        .ToListAsync()
                        .ConfigureAwait(false);
                }

                return await this.CombineWithRemoteResults(new QueryResult(results, new QueryStatsInfo(stats.IndexEtag, stats.IndexTimestamp, stats.TotalResults))).ConfigureAwait(false);
            };
        }
    }
}