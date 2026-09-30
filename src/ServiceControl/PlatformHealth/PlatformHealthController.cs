namespace ServiceControl.PlatformHealth
{
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using ServiceControl.Api.Contracts;
    using ServiceControl.Infrastructure.Auth;

    [ApiController]
    [Route("api")]
    public class PlatformHealthController(PlatformHealthState platformHealthState) : ControllerBase
    {
        [Authorize(Policy = Permissions.ErrorCustomChecksView)]
        [Route("platform-health")]
        [HttpGet]
        public PlatformHealthView Get() => platformHealthState.GetHealth();
    }
}