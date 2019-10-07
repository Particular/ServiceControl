namespace ServiceControl.Audit.Auditing.MessagesView
{
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Infrastructure.Extensions;
    using Raven.Client;
    using ServiceControl.Infrastructure.Extensions;

    class GetAllMessagesApi : ApiBaseNoInput<IList<MessagesView>>
    {
        public GetAllMessagesApi(IDocumentStore documentStore) : base(documentStore)
        {
        }

        protected override async Task<QueryResult<IList<MessagesView>>> Query(HttpRequestMessage request)
        {
            using (var session = Store.OpenAsyncSession())
            {
                var results = await session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                    .IncludeSystemMessagesWhere(request)
                    .Statistics(out var stats)
                    .Sort(request)
                    .Paging(request)
                    .TransformWith<MessagesViewTransformer, MessagesView>()
                    .ToListAsync()
                    .ConfigureAwait(false);

                return new QueryResult<IList<MessagesView>>(results, stats.ToQueryStatsInfo());
            }
        }
    }
}