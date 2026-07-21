namespace ServiceControl.Monitoring
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Infrastructure.Auth;
    using Infrastructure.WebApi;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Primitives;
    using Persistence;
    using Persistence.Infrastructure;

    public class EndpointUpdateModel
    {
        public bool MonitorHeartbeat { get; set; }
    }

    [ApiController]
    [Route("api")]
    public class EndpointsMonitoringController(
        IEndpointInstanceMonitoring monitoring,
        IMonitoringDataStore dataStore)
        : ControllerBase
    {
        [Authorize(Policy = Permissions.ErrorHeartbeatsView)]
        [Route("heartbeats/stats")]
        [HttpGet]
        public EndpointMonitoringStats HeartbeatStats() => monitoring.GetStats();

        [Authorize(Policy = Permissions.ErrorEndpointsView)]
        [Route("endpoints")]
        [HttpGet]
        public EndpointsView[] Endpoints() => monitoring.GetEndpoints();

        // Added as a way for SP to check if operations are supported by the SC API
        // Needs to be anonymous to allow preflight OPTIONS requests from browsers
        [AllowAnonymous]
        [Route("endpoints")]
        [HttpOptions]
        public void GetSupportedOperations()
        {
            Response.Headers.Allow = new StringValues(["GET", "DELETE", "PATCH"]);
            Response.Headers.AccessControlExposeHeaders = "Allow";
        }

        [Authorize(Policy = Permissions.ErrorEndpointsDelete)]
        [Route("endpoints/{endpointId}")]
        [HttpDelete]
        public async Task<IActionResult> DeleteEndpoint(Guid endpointId)
        {
            if (!monitoring.HasEndpoint(endpointId))
            {
                return NotFound();
            }

            await dataStore.Delete(endpointId);

            monitoring.RemoveEndpoint(endpointId);
            return NoContent();
        }

        [Authorize(Policy = Permissions.ErrorEndpointsView)]
        [Route("endpoints/known")]
        [HttpGet]
        public IList<KnownEndpointsView> KnownEndpoints([FromQuery] PagingInfo pagingInfo)
        {
            var knownEndpoints = monitoring.GetKnownEndpoints();

            Response.WithQueryStatsAndPagingInfo(new QueryStatsInfo(string.Empty, knownEndpoints.Count, isStale: false), pagingInfo);
            return knownEndpoints;
        }

        [Authorize(Policy = Permissions.ErrorEndpointsManage)]
        [Route("endpoints/{endpointId}")]
        [HttpPatch]
        public async Task<IActionResult> Monitoring(Guid endpointId, [FromBody] EndpointUpdateModel data)
        {
            if (!monitoring.HasEndpoint(endpointId))
            {
                return NotFound();
            }

            if (data.MonitorHeartbeat)
            {
                await monitoring.EnableMonitoring(endpointId);
            }
            else
            {
                await monitoring.DisableMonitoring(endpointId);
            }

            return Accepted();
        }
    }
}
