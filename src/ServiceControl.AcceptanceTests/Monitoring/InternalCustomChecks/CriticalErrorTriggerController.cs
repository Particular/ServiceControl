namespace ServiceControl.AcceptanceTests.Monitoring.InternalCustomChecks
{
    using System;
    using Microsoft.AspNetCore.Mvc;
    using NServiceBus;

    [ApiController]
    public class CriticalErrorTriggerController(CriticalError error) : ControllerBase
    {
        [Route("criticalerror/trigger")]
        [HttpPost]
        public void Trigger(string message) => error.Raise(message, new Exception());
    }
}