namespace ServiceControl.Monitoring
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using CompositeViews.Messages;
    using Infrastructure.WebApi;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Http.Extensions;
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
        GetKnownEndpointsApi knownEndpointsApi,
        IMonitoringDataStore dataStore)
        : ControllerBase
    {
        [Route("heartbeats/stats")]
        [HttpGet]
        public EndpointMonitoringStats HeartbeatStats() => monitoring.GetStats();

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

        [Route("endpoints/known")]
        [HttpGet]
        public async Task<IList<KnownEndpointsView>> KnownEndpoints([FromQuery] PagingInfo pagingInfo)
        {
            QueryResult<IList<KnownEndpointsView>> result =
                await knownEndpointsApi.Execute(new ScatterGatherContext(pagingInfo), Request.GetEncodedPathAndQuery());

            Response.WithQueryStatsAndPagingInfo(result.QueryStats, pagingInfo);
            return result.Results;
        }

        [Route("endpoints/{endpointId}")]
        [HttpPatch]
        public async Task<IActionResult> Monitoring(Guid endpointId, [FromBody] EndpointUpdateModel data)
        {
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
