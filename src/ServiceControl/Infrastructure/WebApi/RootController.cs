namespace ServiceControl.Infrastructure.WebApi
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Http.Extensions;
    using Microsoft.AspNetCore.Mvc;
    using ServiceControl.Api;
    using ServiceControl.Api.Contracts;

    // This is used for service-to-service communication. This currently needs to be anonymous
    [AllowAnonymous]
    [ApiController]
    [Route("api")]
    public class RootController(IConfigurationApi configurationApi) : ControllerBase
    {
        [Route("")]
        [HttpGet]
        public Task<RootUrls> Urls(CancellationToken cancellationToken = default) => configurationApi.GetUrls(Request.GetDisplayUrl(), cancellationToken);

        [Route("instance-info")]
        [Route("configuration")]
        [HttpGet]
        public Task<object> Config(CancellationToken cancellationToken = default) => configurationApi.GetConfig(cancellationToken);

        [Route("configuration/remotes")]
        [HttpGet]
        public async Task<IActionResult> RemoteConfig(CancellationToken cancellationToken = default) =>
            Ok(await configurationApi.GetRemoteConfigs(cancellationToken));
    }
}