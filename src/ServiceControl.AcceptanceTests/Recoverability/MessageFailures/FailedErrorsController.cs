namespace ServiceControl.AcceptanceTests.Recoverability.MessageFailures
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Web.Http;
    using Infrastructure.WebApi;
    using Operations;
    using Raven.Client;

    public class FailedErrorsCountReponse
    {
        public int Count { get; set; }
    }

    public class FailedErrorsController : ApiController
    {
        internal FailedErrorsController(IDocumentStore store, Lazy<ImportFailedErrors> importFailedAudits)
        {
            this.store = store;
            this.importFailedAudits = importFailedAudits;
        }

        [Route("failederrors/count")]
        [HttpGet]
        public async Task<HttpResponseMessage> GetFailedAuditsCount()
        {
            using (var session = store.OpenAsyncSession())
            {
                var query =
                    session.Query<FailedErrorImport, FailedErrorImportIndex>().Statistics(out var stats);

                var count = await query.CountAsync();

                return Request.CreateResponse(HttpStatusCode.OK, new FailedErrorsCountReponse
                    {
                        Count = count
                    })
                    .WithEtag(stats);
            }
        }

        [Route("failederrors/import")]
        [HttpPost]
        public async Task<HttpResponseMessage> ImportFailedAudits(CancellationToken token)
        {
            var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(token);
            await importFailedAudits.Value.Run(tokenSource);
            return Request.CreateResponse(HttpStatusCode.OK);
        }

        readonly IDocumentStore store;
        readonly Lazy<ImportFailedErrors> importFailedAudits;
    }
}