namespace ServiceControl.CustomChecks
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using System.Web.Http;
    using System.Web.Http.Results;
    using Infrastructure.WebApi;
    using NServiceBus;

    public class CustomCheckController : ApiController
    {
        internal CustomCheckController(ICustomChecksStorage customChecksStorage, IMessageSession messageSession)
        {
            this.messageSession = messageSession;
            this.customChecksStorage = customChecksStorage;
        }

        [Route("customchecks")]
        [HttpGet]
        public async Task<HttpResponseMessage> CustomChecks(string status = null)
        {
            var stats = await customChecksStorage.GetStats(Request, status).ConfigureAwait(false);
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

        ICustomChecksStorage customChecksStorage;
        IMessageSession messageSession;
    }
}