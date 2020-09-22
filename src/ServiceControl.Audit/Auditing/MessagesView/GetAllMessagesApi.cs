namespace ServiceControl.Audit.Auditing.MessagesView
{
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Raven.Client.Documents;

    class GetAllMessagesApi : ApiBaseNoInput<IList<MessagesView>>
    {
        public GetAllMessagesApi(IDocumentStore documentStore) : base(documentStore)
        {
        }

        protected override Task<QueryResult<IList<MessagesView>>> Query(HttpRequestMessage request)
        {
            return Task.FromResult(QueryResult<IList<MessagesView>>.Empty());
            // TODO: RAVEN5 - No Transformers
            //using (var session = Store.OpenAsyncSession())
            //{
            //    var results = await session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
            //        .IncludeSystemMessagesWhere(request)
            //        .Statistics(out var stats)
            //        .Sort(request)
            //        .Paging(request)
            //        .TransformWith<MessagesViewTransformer, MessagesView>()
            //        .ToListAsync()
            //        .ConfigureAwait(false);

            //    return new QueryResult<IList<MessagesView>>(results, stats.ToQueryStatsInfo());
            //}
        }
    }
}