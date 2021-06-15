namespace ServiceControl.Audit.AcceptanceTests.Auditing
{
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Web.Http;
    using Audit.Auditing;
    using Infrastructure.WebApi;
    using NServiceBus;
    using Raven.Client;

    public class FailedAuditsController : ApiController
    {
        internal FailedAuditsController(IMessageSession bus, IDocumentStore store, AuditIngestionComponent auditIngestion)
        {
            this.bus = bus;
            this.store = store;
            this.auditIngestion = auditIngestion;
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
                    .WithEtag(stats);
            }
        }

        [Route("failedaudits/import")]
        [HttpPost]
        public async Task<HttpResponseMessage> ImportFailedAudits(CancellationToken cancellationToken = default)
        {
            var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            await auditIngestion.ImportFailedAudits(bus, tokenSource.Token);
            return Request.CreateResponse(HttpStatusCode.OK);
        }

        readonly IMessageSession bus;
        readonly IDocumentStore store;
        readonly AuditIngestionComponent auditIngestion;
    }

    public class FailedAuditsCountReponse
    {
        public int Count { get; set; }
    }
}