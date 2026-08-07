namespace ServiceControl.EventLog
{
    using System.Collections.Generic;
    using System.Net;
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
            // Passing knownVersion lets the persister skip work it would otherwise waste
            var result = await logDataStore.GetEventLogItems(pagingInfo, Request.GetKnownVersion(), cancellationToken);

            Response.WithPagingLinksAndTotalCount(pagingInfo, result.QueryStats.TotalCount);
            Response.WithEtag(result.QueryStats.Version);

            if (result.NotModified)
            {
                return StatusCode((int)HttpStatusCode.NotModified);
            }

            return Ok(result.Results);
        }
    }
}