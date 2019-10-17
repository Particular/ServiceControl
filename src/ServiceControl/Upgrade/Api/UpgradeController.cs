namespace ServiceControl.Upgrade
{
    using System;
    using System.Net.Http;
    using System.Web.Http;
    using Infrastructure.WebApi;
    using ServiceBus.Management.Infrastructure.Settings;

    public class UpgradeController : ApiController
    {
        internal UpgradeController(Settings settings)
        {
        }
        
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
    public class StaleIndexInfo
    {
        // ReSharper disable once NotAccessedField.Global
        public DateTime? StartedAt;
        // ReSharper disable once NotAccessedField.Global
        public bool InProgress;
    }
}

