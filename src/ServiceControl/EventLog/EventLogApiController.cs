namespace ServiceControl.EventLog
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Infrastructure.WebApi;
    using Microsoft.AspNetCore.Mvc;
    using Persistence.Infrastructure;
    using ServiceControl.Persistence;

    [ApiController]
    public class EventLogApiController(IEventLogDataStore logDataStore) : ControllerBase
    {
        [Route("eventlogitems")]
        [HttpGet]
        public async Task<IList<EventLogItem>> Items([FromQuery] PagingInfo pagingInfo)
        {
            var (results, totalCount, version) = await logDataStore.GetEventLogItems(pagingInfo);

            Response.WithPagingLinksAndTotalCount(pagingInfo, totalCount);
            Response.WithEtag(version);

            return results;
        }
    }
}