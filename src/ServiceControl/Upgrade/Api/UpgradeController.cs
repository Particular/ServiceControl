namespace ServiceControl.Upgrade
{
    using System;
    using System.Net.Http;
    using System.Web.Http;
    using Infrastructure.WebApi;

    public class UpgradeController : ApiController
    {
        [Route("upgrade")]
        [HttpGet]
        public HttpResponseMessage GetUpgradeStatus()
        {
            return Negotiator.FromModel(Request, NotInProgress);
        }

        static readonly StaleIndexInfo NotInProgress = new StaleIndexInfo
        {
            InProgress = false,
            StartedAt = null
        };

    }
    public struct StaleIndexInfo
    {
        // ReSharper disable once NotAccessedField.Global
        public DateTime? StartedAt;
        // ReSharper disable once NotAccessedField.Global
        public bool InProgress;
    }
}

