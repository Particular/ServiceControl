namespace ServiceControl.AcceptanceTests.RavenDB.Recoverability.MessageFailures
{
    using System.Threading.Tasks;
    using Infrastructure.WebApi;
    using Microsoft.AspNetCore.Mvc;
    using Persistence.RavenDB;
    using Raven.Client.Documents;
    using ServiceControl.Recoverability;

    public class FailedMessageRetriesCountReponse
    {
        public long Count { get; set; }
    }

    [ApiController]
    [Route("api")]
    public class FailedMessageRetriesController(IRavenSessionProvider sessionProvider) : ControllerBase
    {
        [Route("failedmessageretries/count")]
        [HttpGet]
        public async Task<FailedMessageRetriesCountReponse> GetFailedMessageRetriesCount()
        {
            using var session = sessionProvider.OpenSession();
            await session.Query<FailedMessageRetry>().Statistics(out var stats).ToListAsync();

            Response.WithEtag(stats.ResultEtag.ToString());

            return new FailedMessageRetriesCountReponse { Count = stats.TotalResults };
        }
    }
}