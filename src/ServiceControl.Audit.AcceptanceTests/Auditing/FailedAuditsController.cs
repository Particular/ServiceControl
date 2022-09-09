namespace ServiceControl.Audit.AcceptanceTests.Auditing
{
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Web.Http;
    using Audit.Auditing;
    using ServiceControl.Audit.Infrastructure.WebApi;
    using ServiceControl.Audit.Persistence;

    class FailedAuditsController : ApiController
    {
        public FailedAuditsController(ImportFailedAudits failedAuditIngestion, IFailedAuditStorage failedAuditStorage)
        {
            this.failedAuditIngestion = failedAuditIngestion;
            this.failedAuditStorage = failedAuditStorage;
        }

        [Route("failedaudits/count")]
        [HttpGet]
        public async Task<HttpResponseMessage> GetFailedAuditsCount()
        {
            var count = await failedAuditStorage.GetFailedAuditsCount();

            return Request.CreateResponse(HttpStatusCode.OK, new FailedAuditsCountReponse
            {
                Count = count
            });
        }

        [Route("failedaudits/import")]
        [HttpPost]
        public async Task<HttpResponseMessage> ImportFailedAudits(CancellationToken cancellationToken = default)
        {
            var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            await failedAuditIngestion.Run(tokenSource.Token);
            return Request.CreateResponse(HttpStatusCode.OK);
        }

        readonly ImportFailedAudits failedAuditIngestion;
        readonly IFailedAuditStorage failedAuditStorage;
    }

    public class FailedAuditsCountReponse
    {
        public int Count { get; set; }
    }
}