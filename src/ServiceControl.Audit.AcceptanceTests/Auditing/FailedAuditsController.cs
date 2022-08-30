namespace ServiceControl.Audit.AcceptanceTests.Auditing
{
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Web.Http;
    using Audit.Auditing;
    using Infrastructure.WebApi;
    using Raven.Client;
    using ServiceControl.Audit.Persistence.RavenDb.Indexes;

    class FailedAuditsController : ApiController
    {
        public FailedAuditsController(IDocumentStore store, ImportFailedAudits failedAuditIngestion)
        {
            this.store = store;
            this.failedAuditIngestion = failedAuditIngestion;
        }

        [Route("failedaudits/count")]
        [HttpGet]
        public async Task<HttpResponseMessage> GetFailedAuditsCount()
        {
            using (var session = store.OpenAsyncSession())
            {
                var query =
                    session.Query<FailedAuditImport, FailedAuditImportIndex>().Statistics(out var stats);

                var count = await query.CountAsync();

                return Request.CreateResponse(HttpStatusCode.OK, new FailedAuditsCountReponse
                {
                    Count = count
                })
                    .WithEtag(stats.IndexEtag.ToString());
            }
        }

        [Route("failedaudits/import")]
        [HttpPost]
        public async Task<HttpResponseMessage> ImportFailedAudits(CancellationToken cancellationToken = default)
        {
            var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            await failedAuditIngestion.Run(tokenSource.Token);
            return Request.CreateResponse(HttpStatusCode.OK);
        }

        readonly IDocumentStore store;
        readonly ImportFailedAudits failedAuditIngestion;
    }

    public class FailedAuditsCountReponse
    {
        public int Count { get; set; }
    }
}