namespace ServiceControl.MessageFailures.Api
{
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using System.Web.Http;
    using Infrastructure.Extensions;
    using Infrastructure.WebApi;
    using Raven.Client.Documents;

    public class GetAllErrorsController : ApiController
    {
        internal GetAllErrorsController(IDocumentStore documentStore)
        {
            this.documentStore = documentStore;
        }

        [Route("errors")]
        [HttpGet]
        public async Task<HttpResponseMessage> ErrorsGet()
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                var results = await session.Advanced
                    .AsyncDocumentQuery<FailedMessageViewIndex.SortAndFilterOptions, FailedMessageViewIndex>()
                    .Statistics(out var stats)
                    .FilterByStatusWhere(Request)
                    .FilterByLastModifiedRange(Request)
                    .FilterByQueueAddress(Request)
                    .Sort(Request)
                    .Paging(Request)
                    //TODO:RAVEN5 missing API transformers
                    //.SetResultTransformer(new FailedMessageViewTransformer().TransformerName)
                    .SelectFields<FailedMessageView>()
                    .ToListAsync()
                    .ConfigureAwait(false);

                return Negotiator
                    .FromModel(Request, results)
                    .WithPagingLinksAndTotalCount(stats.TotalResults, Request)
                    .WithEtag(stats);
            }
        }

        [Route("errors")]
        [HttpHead]
        public async Task<HttpResponseMessage> ErrorsHead()
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                var queryResult = await session.Advanced
                    .AsyncDocumentQuery<FailedMessageViewIndex.SortAndFilterOptions, FailedMessageViewIndex>()
                    .FilterByStatusWhere(Request)
                    .FilterByLastModifiedRange(Request)
                    .FilterByQueueAddress(Request)
                    .GetQueryResultAsync()
                    .ConfigureAwait(false);

                var response = Request.CreateResponse(HttpStatusCode.OK);

                return response
                    .WithTotalCount(queryResult.TotalResults)
                    .WithEtag($"{queryResult.ResultEtag}");
            }
        }

        [Route("endpoints/{endpointname}/errors")]
        [HttpGet]
        public async Task<HttpResponseMessage> ErrorsByEndpointName(string endpointName)
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                var results = await session.Advanced
                    .AsyncDocumentQuery<FailedMessageViewIndex.SortAndFilterOptions, FailedMessageViewIndex>()
                    .Statistics(out var stats)
                    .FilterByStatusWhere(Request)
                    .AndAlso()
                    .WhereEquals("ReceivingEndpointName", endpointName)
                    .FilterByLastModifiedRange(Request)
                    .Sort(Request)
                    .Paging(Request)
                    //TODO:RAVEN5 missing API transformers and such
                    //.SetResultTransformer(new FailedMessageViewTransformer().TransformerName)
                    .SelectFields<FailedMessageView>()
                    .ToListAsync()
                    .ConfigureAwait(false);

                return Negotiator
                    .FromModel(Request, results)
                    .WithPagingLinksAndTotalCount(stats.TotalResults, Request)
                    .WithEtag(stats);
            }
        }

        [Route("errors/summary")]
        [HttpGet]
        public async Task<HttpResponseMessage> ErrorsSummary()
        {
            await Task.Yield();
            return default;

            // using (var session = documentStore.OpenAsyncSession())
            // {
            //     var facetResults = await session.Query<FailedMessage, FailedMessageFacetsIndex>()
            //         .ToFacetsAsync(new List<Facet>
            //         {
            //             new Facet
            //             {
            //                 Name = "Name",
            //                 DisplayName = "Endpoints"
            //             },
            //             new Facet
            //             {
            //                 Name = "Host",
            //                 DisplayName = "Hosts"
            //             },
            //             new Facet
            //             {
            //                 Name = "MessageType",
            //                 DisplayName = "Message types"
            //             }
            //         })
            //         .ConfigureAwait(false);
            //
            //     return Negotiator.FromModel(Request, facetResults.Results);
            // }
        }

        readonly IDocumentStore documentStore;
    }
}