namespace ServiceControl.EventLog
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Infrastructure.Auth;
    using Infrastructure.WebApi;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using Persistence.Infrastructure;
    using ServiceControl.Persistence;

    [ApiController]
    [Route("api")]
    public class EventLogApiController(IEventLogDataStore logDataStore) : ControllerBase
    {
        [Authorize(Policy = Permissions.ErrorEventLogView)]
        [Route("eventlogitems")]
        [HttpGet]
        public async Task<ActionResult<IList<EventLogItemView>>> Items([FromQuery] PagingInfo pagingInfo, CancellationToken cancellationToken = default)
        {
            var result = await logDataStore.GetEventLogItems(pagingInfo, cancellationToken);

            Response.WithQueryStatsAndPagingInfo(result.QueryStats, pagingInfo);

            return Ok(result.Results);
        }
    }
}