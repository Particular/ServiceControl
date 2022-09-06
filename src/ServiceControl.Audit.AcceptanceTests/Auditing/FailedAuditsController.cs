namespace ServiceControl.Audit.AcceptanceTests.Auditing
{
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Web.Http;
    using Audit.Auditing;
    //using Infrastructure.WebApi;
    //using Raven.Client;
    //using ServiceControl.Audit.Persistence.RavenDb.Indexes;

    class FailedAuditsController : ApiController
    {
        public FailedAuditsController(ImportFailedAudits failedAuditIngestion)
        {
            this.failedAuditIngestion = failedAuditIngestion;
        }

        [Route("failedaudits/count")]
        [HttpGet]
        public Task<HttpResponseMessage> GetFailedAuditsCount()
        {
            //using (var session = store.OpenAsyncSession())
            //{
            //    //var query =
            //    //    session.Query<FailedAuditImport, FailedAuditImportIndex>().Statistics(out var stats);

            //    //var count = await query.CountAsync();

            //    return Request.CreateResponse(HttpStatusCode.OK, new FailedAuditsCountReponse
            //    {
            //        Count = 0
            //    })
            //        .WithEtag("");
            //}

            return Task.FromResult<HttpResponseMessage>(null);
        }

        [Route("failedaudits/import")]
        [HttpPost]
        public async Task<HttpResponseMessage> ImportFailedAudits(CancellationToken cancellationToken = default)
        {
            var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            await failedAuditIngestion.Run(tokenSource.Token);
            return Request.CreateResponse(HttpStatusCode.OK);
        }

        //readonly IDocumentStore store;
        readonly ImportFailedAudits failedAuditIngestion;
    }

    public class FailedAuditsCountReponse
    {
        public int Count { get; set; }
    }
}