namespace ServiceControl.CompositeViews.MessageCounting
{
    using System.Net.Http;
    using System.Threading.Tasks;
    using System.Web.Http;
    using ServiceControl.CompositeViews.Messages;

    class AuditCountsController : ApiController
    {
        readonly GetAuditCountsApi getCountsApi;

        public AuditCountsController(GetAuditCountsApi getCountsApi)
        {
            this.getCountsApi = getCountsApi;
        }

        [Route("audit-counts")]
        [HttpGet]
        public Task<HttpResponseMessage> GetAuditCounts()
        {
            return getCountsApi.Execute(this, NoInput.Instance);
        }
    }
}
