namespace ServiceControl.MessageFailures.Api
{
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using System.Web.Http;
    using Infrastructure.WebApi;
    using Persistence.Infrastructure;
    using ServiceControl.Persistence;

    class QueueAddressController : ApiController
    {
        public QueueAddressController(IQueueAddressStore dataStore)
        {
            this.dataStore = dataStore;
        }

        [Route("errors/queues/addresses")]
        [HttpGet]
        public async Task<HttpResponseMessage> GetAddresses()
        {
            var pagingInfo = Request.GetPagingInfo();
            var result = await dataStore.GetAddresses(pagingInfo)
                .ConfigureAwait(false);

            return Negotiator
                .FromModel(Request, result)
                .WithPagingLinksAndTotalCount(result.QueryStats.TotalCount, Request)
                .WithEtag(result.QueryStats.ETag);
        }

        [Route("errors/queues/addresses/search/{search}")]
        [HttpGet]
        public async Task<HttpResponseMessage> GetAddressesBySearchTerm(string search = null)
        {
            if (string.IsNullOrWhiteSpace(search))
            {
                return Request.CreateResponse(HttpStatusCode.BadRequest);
            }

            var pagingInfo = Request.GetPagingInfo();
            var result = await dataStore.GetAddressesBySearchTerm(search, pagingInfo)
                .ConfigureAwait(false);

            return Negotiator
                .FromModel(Request, result.Results)
                .WithPagingLinksAndTotalCount(result.QueryStats.TotalCount, Request)
                .WithEtag(result.QueryStats.ETag);
        }

        readonly IQueueAddressStore dataStore;
    }
}