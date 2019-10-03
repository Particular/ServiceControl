namespace ServiceControl.Monitoring.Http
{
    using System.Web.Http;
    using System.Web.Http.Results;

    public class RootController : ApiController
    {
        [Route("")]
        public OkNegotiatedContentResult<MonitoringInstanceModel> Get()
        {
            var model = new MonitoringInstanceModel
            {
                InstanceType = "monitoring",
                Version = VersionInfo.FileVersion
            };

            return Ok(model);
        }

        public class MonitoringInstanceModel
        {
            public string InstanceType { get; set; }
            public string Version { get; set; }
        }
    }
}