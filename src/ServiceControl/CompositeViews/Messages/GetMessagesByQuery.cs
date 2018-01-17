namespace ServiceControl.CompositeViews.Messages
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Infrastructure.Extensions;
    using Raven.Client;
    using Raven.Client.Linq;
    using ServiceBus.Management.Infrastructure.Settings;

    public class GetMessagesByQuery : MessageViewQueryAggregatingModule
    {
        public GetMessagesByQuery(Settings settings) 
            : base(settings)
        {
            Get["/messages/search", true] = (_, token) =>
            {
                string keyword = Request.Query.q;

                return SearchByKeyword(keyword);
            };

            Get["/messages/search/{keyword*}", true] = (parameters, token) =>
            {
                string keyword = parameters.keyword;
                keyword = keyword?.Replace("/", @"\");
                return SearchByKeyword(keyword);
            };

            Get["/endpoints/{name}/messages/search", true] = (parameters, token) =>
            {
                string keyword = Request.Query.q;
                string name = parameters.name;

                return SearchByKeyword(keyword, name);
            };

            Get["/endpoints/{name}/messages/search/{keyword}", true] = (parameters, token) =>
            {
                string keyword = parameters.keyword;
                string name = parameters.name;

                return SearchByKeyword(keyword, name);
            };
        }

        async Task<dynamic> SearchByKeyword(string keyword, string name)
        {
            RavenQueryStatistics stats;
            IList<MessagesView> results;

            using (var session = Store.OpenAsyncSession())
            {
                results = await session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                    .Statistics(out stats)
                    .Search(x => x.Query, keyword)
                    .Where(m => m.ReceivingEndpointName == name)
                    .Sort(Request)
                    .Paging(Request)
                    .TransformWith<MessagesViewTransformer, MessagesView>()
                    .ToListAsync()
                    .ConfigureAwait(false);
            }

            return await CombineWithRemoteResults(results, stats.TotalResults, stats.IndexEtag, stats.IndexTimestamp).ConfigureAwait(false);
        }

        async Task<dynamic> SearchByKeyword(string keyword)
        {
            RavenQueryStatistics stats;
            IList<MessagesView> results;
            using (var session = Store.OpenAsyncSession())
            {
                results = await session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                    .Statistics(out stats)
                    .Search(x => x.Query, keyword)
                    .Sort(Request)
                    .Paging(Request)
                    .TransformWith<MessagesViewTransformer, MessagesView>()
                    .ToListAsync()
                    .ConfigureAwait(false);
            }

            return await CombineWithRemoteResults(results, stats.TotalResults, stats.IndexEtag, stats.IndexTimestamp).ConfigureAwait(false);
        }
    }
}