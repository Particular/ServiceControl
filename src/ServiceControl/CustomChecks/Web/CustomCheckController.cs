namespace ServiceControl.CustomChecks
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using System.Web.Http;
    using System.Web.Http.Results;
    using Infrastructure;
    using Infrastructure.WebApi;
    using NServiceBus;
    using ServiceControl.Persistence;

    class CustomCheckController : ApiController
    {
        public CustomCheckController(ICustomChecksDataStore customChecksDataStore, IMessageSession messageSession)
        {
            this.messageSession = messageSession;
            this.customChecksDataStore = customChecksDataStore;
        }

        [Route("customchecks")]
        [HttpGet]
        public async Task<HttpResponseMessage> CustomChecks(string status = null)
        {
            var paging = Request.GetPagingInfo();
            var stats = await customChecksDataStore.GetStats(paging, status).ConfigureAwait(false);
            return Negotiator
                .FromModel(Request, stats.Results)
                .WithPagingLinksAndTotalCount(stats.QueryStats.TotalCount, Request)
                .WithEtag(stats.QueryStats.ETag);
        }

        [Route("customchecks/{id}")]
        [HttpDelete]
        public async Task<StatusCodeResult> Delete(Guid id)
        {
            await messageSession.SendLocal(new DeleteCustomCheck { Id = id }).ConfigureAwait(false);

            return StatusCode(HttpStatusCode.Accepted);
        }

        ICustomChecksDataStore customChecksDataStore;
        IMessageSession messageSession;
    }
}