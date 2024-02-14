namespace ServiceControl.Audit.AcceptanceTests.Auditing
{
    using System.Threading;
    using System.Threading.Tasks;
    using Audit.Auditing;
    using Microsoft.AspNetCore.Mvc;
    using Persistence;

    [ApiController]
    [Route("api")]
    public class FailedAuditsController(ImportFailedAudits failedAuditIngestion, IFailedAuditStorage failedAuditStorage)
        : ControllerBase
    {
        [Route("failedaudits/count")]
        [HttpGet]
        public async Task<FailedAuditsCountReponse> GetFailedAuditsCount()
        {
            var count = await failedAuditStorage.GetFailedAuditsCount();

            return new FailedAuditsCountReponse { Count = count };
        }

        [Route("failedaudits/import")]
        [HttpPost]
        public async Task ImportFailedAudits(CancellationToken cancellationToken = default)
        {
            var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            await failedAuditIngestion.Run(tokenSource.Token);
        }
    }

    public class FailedAuditsCountReponse
    {
        public int Count { get; set; }
    }
}