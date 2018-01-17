namespace ServiceControl.CompositeViews.Messages
{
    using System.Collections.Generic;
    using Infrastructure.Extensions;
    using Raven.Client;
    using Raven.Client.Linq;
    using ServiceBus.Management.Infrastructure.Settings;

    public class GetMessagesByConversation : MessageViewQueryAggregatingModule
    {
        public GetMessagesByConversation(Settings settings) 
            : base(settings)
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

                return await CombineWithRemoteResults(results, stats.TotalResults, stats.IndexEtag, stats.IndexTimestamp).ConfigureAwait(false);
            };
        }
    }
}