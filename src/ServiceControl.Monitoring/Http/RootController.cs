namespace ServiceControl.Monitoring.Http
{
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Primitives;

    [ApiController]
    public class RootController : ControllerBase
    {
        // Root endpoint returns instance metadata and must remain accessible for discovery and service-to-service calls
        [AllowAnonymous]
        [Route("")]
        [HttpGet]
        public ActionResult<MonitoringInstanceModel> Get()
        {
            var model = new MonitoringInstanceModel
            {
                InstanceType = "monitoring",
                Version = VersionInfo.FileVersion
            };

            return Ok(model);
        }

        [AllowAnonymous]
        [Route("")]
        [HttpOptions]
        public void GetSupportedOperations()
        {
            Response.Headers.Allow = new StringValues(["GET", "DELETE", "PATCH"]);
            Response.Headers.AccessControlExposeHeaders = "Allow";
        }

        public class MonitoringInstanceModel
        {
            public string InstanceType { get; set; }
            public string Version { get; set; }
        }
    }
}