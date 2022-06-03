namespace ServiceControl.EventLog
{
    using System.Linq;
    using System.Net.Http;
    using System.Threading.Tasks;
    using System.Web.Http;
    using Infrastructure.Extensions;
    using Infrastructure.WebApi;
    using Raven.Client;

    public class EventLogApiController : ApiController
    {
        public EventLogApiController(IDocumentStore documentStore)
        {
            this.documentStore = documentStore;
        }

        [Route("eventlogitems")]
        [HttpGet]
        public async Task<HttpResponseMessage> Items()
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                var results = await session.Query<EventLogItem>().Statistics(out var stats).OrderByDescending(p => p.RaisedAt)
                    .Paging(Request)
                    .ToListAsync()
                    .ConfigureAwait(false);

                return Negotiator.FromModel(Request, results)
                    .WithPagingLinksAndTotalCount(stats.TotalResults, Request)
                    .WithEtag(stats);
            }
        }

        readonly IDocumentStore documentStore;
    }
}