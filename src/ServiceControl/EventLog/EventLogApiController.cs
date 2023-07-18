namespace ServiceControl.EventLog
{
    using System.Net.Http;
    using System.Threading.Tasks;
    using System.Web.Http;
    using Infrastructure.WebApi;
    using Persistence.Infrastructure;
    using ServiceControl.Persistence;

    class EventLogApiController : ApiController
    {
        public EventLogApiController(IEventLogDataStore eventLogDataStore)
        {
            this.eventLogDataStore = eventLogDataStore;
        }

        [Route("eventlogitems")]
        [HttpGet]
        public async Task<HttpResponseMessage> Items()
        {
            var pagingInfo = Request.GetPagingInfo();

            var (results, totalCount, version) = await eventLogDataStore.GetEventLogItems(pagingInfo).ConfigureAwait(false);


            return Negotiator.FromModel(Request, results)
                .WithPagingLinksAndTotalCount(totalCount, Request)
                .WithEtag(version);
        }

        readonly IEventLogDataStore eventLogDataStore;
    }
}