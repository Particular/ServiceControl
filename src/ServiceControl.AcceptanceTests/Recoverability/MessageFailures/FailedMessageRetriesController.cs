namespace ServiceControl.AcceptanceTests.Recoverability.MessageFailures
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Web.Http;
    using Infrastructure.WebApi;
    using Operations;
    using Raven.Client;
    using ServiceControl.Recoverability;

    public class FailedMessageRetriesCountReponse
    {
        public int Count { get; set; }
    }

    public class FailedMessageRetriesController : ApiController
    {
        internal FailedMessageRetriesController(IDocumentStore store)
        {
            this.store = store;
        }

        [Route("failedmessageretries/count")]
        [HttpGet]
        public async Task<HttpResponseMessage> GetFailedMessageRetriesCount()
        {
            using (var session = store.OpenAsyncSession())
            {
                var query =
                    session.Query<FailedMessageRetry>().Statistics(out var stats);

                var count = await query.CountAsync();

                return Request.CreateResponse(HttpStatusCode.OK, new FailedMessageRetriesCountReponse
                {
                    Count = count
                })
                .WithEtag(stats);
            }
        }

        readonly IDocumentStore store;
    }
}