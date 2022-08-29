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
    using Raven.Client.Embedded;
    using Infrastructure.RavenDB.Expiration;

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
                    .WithEtag(stats);
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
        public Task<HttpResponseMessage> ForceErrorMessageCleanerRun(CancellationToken cancellationToken = default)
        {
            new ExpiryErrorMessageIndex().Execute(store);
            WaitForIndexes(store);

            ErrorMessageCleaner.Clean(1000, ((EmbeddableDocumentStore)store).DocumentDatabase, DateTime.Now, cancellationToken);
            WaitForIndexes(store);

            return Task.FromResult(Request.CreateResponse(HttpStatusCode.OK));
        }

        static void WaitForIndexes(IDocumentStore store)
        {
            SpinWait.SpinUntil(() => store.DatabaseCommands.GetStatistics().StaleIndexes.Length == 0, TimeSpan.FromSeconds(10));
        }

        readonly IDocumentStore store;
        readonly ImportFailedErrors importFailedErrors;
    }
}