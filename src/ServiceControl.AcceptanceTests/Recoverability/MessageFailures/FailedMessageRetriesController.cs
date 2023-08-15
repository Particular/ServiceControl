﻿namespace ServiceControl.AcceptanceTests.Recoverability.MessageFailures
{
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using System.Web.Http;
    using Infrastructure.WebApi;
    using Raven.Client;
    using ServiceControl.Recoverability;

    public class FailedMessageRetriesCountReponse
    {
        public int Count { get; set; }
    }

    class FailedMessageRetriesController : ApiController
    {
        public FailedMessageRetriesController(IDocumentStore store)
        {
            this.store = store;
        }

        [Route("failedmessageretries/count")]
        [HttpGet]
        public async Task<HttpResponseMessage> GetFailedMessageRetriesCount()
        {
            using (var session = store.OpenAsyncSession())
            {
                await session.Query<FailedMessageRetry>().Statistics(out var stats).ToListAsync();

                return Request.CreateResponse(HttpStatusCode.OK, new FailedMessageRetriesCountReponse
                {
                    Count = stats.TotalResults
                })
                    .WithEtag(stats.IndexEtag);
            }
        }

        readonly IDocumentStore store;
    }
}