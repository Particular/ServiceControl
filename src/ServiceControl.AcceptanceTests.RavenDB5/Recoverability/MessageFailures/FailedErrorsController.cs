namespace ServiceControl.AcceptanceTests.RavenDB.Recoverability.MessageFailures
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Web.Http;
    using Infrastructure.WebApi;
    using Operations;
    using Raven.Client.Documents;
    using Raven.Client.Documents.Operations;

    public class FailedErrorsCountReponse
    {
        public int Count { get; set; }
    }

    class FailedErrorsController : ApiController
    {
        public FailedErrorsController(IDocumentStore store, ImportFailedErrors importFailedErrors)
        {
            this.store = store;
            this.importFailedErrors = importFailedErrors;
        }

        [Route("failederrors/count")]
        [HttpGet]
        public async Task<HttpResponseMessage> GetFailedErrorsCount()
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
                    .WithEtag(stats.ResultEtag.ToString());
            }
        }

        [Route("failederrors/import")]
        [HttpPost]
        public async Task<HttpResponseMessage> ImportFailedErrors(CancellationToken cancellationToken = default)
        {
            var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            await importFailedErrors.Run(tokenSource.Token);
            return Request.CreateResponse(HttpStatusCode.OK);
        }

        [Route("failederrors/forcecleanerrors")]
        [HttpPost]
        public Task<HttpResponseMessage> ForceErrorMessageCleanerRun()
        {
            // TODO: Is there a way to force the Raven5 expiration to happen? Or does it just happen? Won't be able to tell until we redesign that.

            // May not even need WaitForIndexes given Raven5 implementation isn't index-based
            WaitForIndexes(store);

            return Task.FromResult(Request.CreateResponse(HttpStatusCode.OK));
        }

        static void WaitForIndexes(IDocumentStore store)
        {
            SpinWait.SpinUntil(() => store.Maintenance.Send(new GetStatisticsOperation()).StaleIndexes.Length == 0, TimeSpan.FromSeconds(10));
        }

        readonly IDocumentStore store;
        readonly ImportFailedErrors importFailedErrors;
    }
}