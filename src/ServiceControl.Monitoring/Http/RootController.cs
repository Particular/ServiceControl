namespace ServiceControl.Monitoring.Http
{
    using System.Net;
    using System.Net.Http;
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

        [Route("")]
        [HttpOptions]
        public HttpResponseMessage GetSupportedOperations()
        {
            var response = new HttpResponseMessage(HttpStatusCode.NoContent)
            {
                Content = new ByteArrayContent(new byte[] { }) //need to force empty content to avoid null reference when adding headers below :(
            };

            response.Content.Headers.Allow.Add("GET");
            response.Content.Headers.Allow.Add("DELETE");
            response.Content.Headers.Allow.Add("PATCH");
            response.Content.Headers.Add("Access-Control-Expose-Headers", "Allow");

            return response;
        }

        public class MonitoringInstanceModel
        {
            public string InstanceType { get; set; }
            public string Version { get; set; }
        }
    }
}