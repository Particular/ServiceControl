namespace ServiceControl.Monitoring.Http.Diagrams
{
    using Microsoft.AspNetCore.Mvc;
    using ServiceControl.Monitoring.Infrastructure.Api;

    [ApiController]
    public class DiagramApiController(IEndpointMetricsApi endpointMetricsApi) : ControllerBase
    {
        [Route("monitored-endpoints")]
        [HttpGet]
        public MonitoredEndpoint[] GetAllEndpointsMetrics([FromQuery] int? history = null) => endpointMetricsApi.GetAllEndpointsMetrics(history);

        [Route("monitored-endpoints/{endpointName}")]
        [HttpGet]
        public ActionResult<MonitoredEndpointDetails> GetSingleEndpointMetrics(string endpointName, [FromQuery] int? history = null) => endpointMetricsApi.GetSingleEndpointMetrics(endpointName, history);

        [Route("monitored-instance/{endpointName}/{instanceId}")]
        [HttpDelete]
        public IActionResult DeleteEndpointInstance(string endpointName, string instanceId)
        {
            endpointMetricsApi.DeleteEndpointInstance(endpointName, instanceId);

            return Ok();
        }

        [Route("monitored-endpoints/disconnected")]
        [HttpGet]
        public ActionResult<int> DisconnectedEndpointCount() => endpointMetricsApi.DisconnectedEndpointCount();
    }
}
