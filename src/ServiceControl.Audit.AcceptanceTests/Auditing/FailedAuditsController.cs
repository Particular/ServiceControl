namespace ServiceControl.Audit.AcceptanceTests.Auditing
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Web.Http;
    using Audit.Auditing;
    using Infrastructure.WebApi;
    using Raven.Client;

    public class FailedAuditsController : ApiController
    {
        internal FailedAuditsController(IDocumentStore store, Lazy<ImportFailedAudits> importFailedAudits)
        {
            this.store = store;
            this.importFailedAudits = importFailedAudits;
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
        public async Task<HttpResponseMessage> ImportFailedAudits(CancellationToken token)
        {
            var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(token);
            await importFailedAudits.Value.Run(tokenSource);
            return Request.CreateResponse(HttpStatusCode.OK);
        }

        readonly IDocumentStore store;
        readonly Lazy<ImportFailedAudits> importFailedAudits;
    }

    public class FailedAuditsCountReponse
    {
        public int Count { get; set; }
    }
}