namespace ServiceControl.AcceptanceTests.RavenDB.Recoverability.MessageFailures
{
    using System.Threading.Tasks;
    using Infrastructure.WebApi;
    using Microsoft.AspNetCore.Mvc;
    using Raven.Client.Documents;
    using ServiceControl.Recoverability;

    public class FailedMessageRetriesCountReponse
    {
        public int Count { get; set; }
    }

    [ApiController]
    [Route("api")]
    public class FailedMessageRetriesController(IDocumentStore store) : ControllerBase
    {
        [Route("failedmessageretries/count")]
        [HttpGet]
        public async Task<FailedMessageRetriesCountReponse> GetFailedMessageRetriesCount()
        {
            using var session = store.OpenAsyncSession();
            await session.Query<FailedMessageRetry>().Statistics(out var stats).ToListAsync();

            Response.WithEtag(stats.ResultEtag.ToString());

            return new FailedMessageRetriesCountReponse { Count = stats.TotalResults };
        }
    }
}