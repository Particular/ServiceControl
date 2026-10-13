namespace ServiceControl.PlatformHealth
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Http.Extensions;
    using Microsoft.AspNetCore.Mvc;
    using ServiceControl.Api;
    using ServiceControl.Api.Contracts;
    using ServiceControl.Infrastructure.Auth;

    [ApiController]
    [Route("api")]
    public class PlatformHealthController(IPlatformHealthApi platformHealthApi) : ControllerBase
    {
        [Authorize(Policy = Permissions.ErrorCustomChecksView)]
        [Route("platform-health")]
        [HttpGet]
        [ProducesResponseType(typeof(PlatformHealthView), StatusCodes.Status200OK)]
        public Task<PlatformHealthView> Get(CancellationToken cancellationToken = default) =>
            platformHealthApi.GetHealth(UriHelper.BuildAbsolute(Request.Scheme, Request.Host, Request.PathBase, "/api/"), cancellationToken);
    }
}