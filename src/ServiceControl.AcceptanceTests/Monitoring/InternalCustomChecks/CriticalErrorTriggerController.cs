namespace ServiceControl.AcceptanceTests.Monitoring.InternalCustomChecks
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using System.Web.Http;
    using NServiceBus;

    class CriticalErrorTriggerController : ApiController
    {
        CriticalError criticalError;

        public CriticalErrorTriggerController(CriticalError criticalError)
        {
            this.criticalError = criticalError;
        }

        [Route("criticalerror/trigger")]
        [HttpPost]
        public Task<HttpResponseMessage> Trigger(string message)
        {
            criticalError.Raise(message, new Exception());
            return Task.FromResult(Request.CreateResponse(HttpStatusCode.OK));
        }
    }
}