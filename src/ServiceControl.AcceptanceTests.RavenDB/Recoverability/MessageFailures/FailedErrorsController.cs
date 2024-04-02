namespace ServiceControl.AcceptanceTests.RavenDB.Recoverability.MessageFailures
{
    using System.Threading;
    using System.Threading.Tasks;
    using Infrastructure.WebApi;
    using Microsoft.AspNetCore.Mvc;
    using Operations;
    using Persistence.RavenDB;
    using Raven.Client.Documents;

    public class FailedErrorsCountReponse
    {
        public int Count { get; set; }
    }

    [ApiController]
    [Route("api")]
    public class FailedErrorsController(IRavenSessionProvider sessionProvider, ImportFailedErrors failedErrors)
        : ControllerBase
    {
        [Route("failederrors/count")]
        [HttpGet]
        public async Task<FailedErrorsCountReponse> GetFailedErrorsCount()
        {
            using var session = sessionProvider.OpenSession();
            var query =
                session.Query<FailedErrorImport, FailedErrorImportIndex>().Statistics(out var stats);

            var count = await query.CountAsync();

            Response.WithEtag(stats.ResultEtag.ToString());

            return new FailedErrorsCountReponse { Count = count };
        }

        [Route("failederrors/import")]
        [HttpPost]
        public async Task ImportFailedErrors(CancellationToken cancellationToken = default)
        {
            var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            await failedErrors.Run(tokenSource.Token);
        }
    }
}