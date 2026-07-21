namespace ServiceControl.Audit.Monitoring
{
    using System.Collections.Generic;
    using Auditing.MessagesView;
    using Infrastructure;
    using Infrastructure.WebApi;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using ServiceControl.Infrastructure.Auth;

    [ApiController]
    [Route("api")]
    public class KnownEndpointsController : ControllerBase
    {
        // Backwards-compatibility stub: the audit instance no longer stores known endpoints, but older
        // primary instances scatter-gather this route and expect a valid result.
        [Authorize(Policy = Permissions.AuditEndpointView)]
        [Route("endpoints/known")]
        [HttpGet]
        public IList<KnownEndpointsView> GetAll([FromQuery] PagingInfo pagingInfo)
        {
            Response.WithQueryStatsAndPagingInfo(QueryStatsInfo.Zero, pagingInfo);
            return [];
        }
    }
}
