namespace ServiceControl.MessageFailures.Api
{
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using System.Web.Http;
    using Infrastructure.Extensions;
    using Infrastructure.WebApi;
    using Raven.Client.Documents;

    public class QueueAddressController : ApiController
    {
        internal QueueAddressController(IDocumentStore documentStore)
        {
            this.documentStore = documentStore;
        }

        [Route("errors/queues/addresses")]
        [HttpGet]
        public async Task<HttpResponseMessage> GetAddresses()
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                var addresses = await session
                    .Query<QueueAddress, QueueAddressIndex>()
                    .Statistics(out var stats)
                    .Paging(Request)
                    .ToListAsync()
                    .ConfigureAwait(false);

                return Negotiator
                    .FromModel(Request, addresses)
                    .WithPagingLinksAndTotalCount(stats.TotalResults, Request)
                    .WithEtag(stats);
            }
        }

        [Route("errors/queues/addresses/search/{search}")]
        [HttpGet]
        public async Task<HttpResponseMessage> GetAddressesBySearchTerm(string search = null)
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                if (string.IsNullOrWhiteSpace(search))
                {
                    return Request.CreateResponse(HttpStatusCode.BadRequest);
                }

                var failedMessageQueues =
                    await session.Query<QueueAddress, QueueAddressIndex>()
                        .Statistics(out var stats)
                        .Where(q => q.PhysicalAddress.StartsWith(search))
                        .OrderBy(q => q.PhysicalAddress)
                        .Paging(Request)
                        .ToListAsync()
                        .ConfigureAwait(false);

                return Negotiator
                    .FromModel(Request, failedMessageQueues)
                    .WithPagingLinksAndTotalCount(stats.TotalResults, Request)
                    .WithEtag(stats);
            }
        }

        readonly IDocumentStore documentStore;
    }
}