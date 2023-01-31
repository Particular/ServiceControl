namespace ServiceControl.Audit.Auditing.MessageCounting
{
    using System.Net.Http;
    using System.Threading.Tasks;
    using System.Web.Http;
    using ServiceControl.Audit.Auditing.MessagesView;

    class AuditCountsController : ApiController
    {
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

        readonly GetAuditCountsApi getCountsApi;
    }
}
