namespace ServiceControl.Monitoring.Http.Diagrams
{
    using Infrastructure.Api;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using ServiceControl.Infrastructure.Auth;

    [ApiController]
    public class DiagramApiController(IEndpointMetricsApi endpointMetricsApi) : ControllerBase
    {
        [Authorize(Policy = Permissions.MonitoringEndpointView)]
        [Route("monitored-endpoints")]
        [HttpGet]
        public MonitoredEndpoint[] GetAllEndpointsMetrics([FromQuery] int? history = null) =>
            endpointMetricsApi.GetAllEndpointsMetrics(history);

        [Authorize(Policy = Permissions.MonitoringEndpointView)]
        [Route("monitored-endpoints/{endpointName}")]
        [HttpGet]
        public ActionResult<MonitoredEndpointDetails> GetSingleEndpointMetrics(string endpointName,
            [FromQuery] int? history = null) => endpointMetricsApi.GetSingleEndpointMetrics(endpointName, history);

        [Authorize(Policy = Permissions.MonitoringEndpointDelete)]
        [Route("monitored-instance/{endpointName}/{instanceId}")]
        [HttpDelete]
        public IActionResult DeleteEndpointInstance(string endpointName, string instanceId)
        {
            endpointMetricsApi.DeleteEndpointInstance(endpointName, instanceId);

            return Ok();
        }

        [Authorize(Policy = Permissions.MonitoringEndpointView)]
        [Route("monitored-endpoints/disconnected")]
        [HttpGet]
        public ActionResult<int> DisconnectedEndpointCount() => endpointMetricsApi.DisconnectedEndpointCount();
    }
}