﻿namespace ServiceControl.Monitoring
{
    using System;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Primitives;
    using ServiceControl.Persistence;

    public class EndpointUpdateModel
    {
        public bool MonitorHeartbeat { get; set; }
    }

    [ApiController]
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

        //Added as a way for SP to check if the DELETE operation is supported by the SC API
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
        public Task<HttpResponseMessage> KnownEndpoints() => knownEndpointsApi.Execute(this);


        [Route("endpoints/{endpointId}")]
        [HttpPatch]
        public async Task<IActionResult> Foo(Guid endpointId, EndpointUpdateModel data)
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
