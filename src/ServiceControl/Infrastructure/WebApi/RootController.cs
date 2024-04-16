namespace ServiceControl.Infrastructure.WebApi
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Http.Extensions;
    using Microsoft.AspNetCore.Mvc;
    using ServiceControl.Api;
    using ServiceControl.Api.Contracts;

    [ApiController]
    [Route("api")]
    public class RootController(IConfigurationApi configurationApi) : ControllerBase
    {
        [Route("")]
        [HttpGet]
        public async Task<RootUrls> Urls(CancellationToken cancellationToken) => await configurationApi.GetUrls(Request.GetDisplayUrl() + "/", cancellationToken);

        [Route("instance-info")]
        [Route("configuration")]
        [HttpGet]
        public async Task<object> Config(CancellationToken cancellationToken) => await configurationApi.GetConfig(cancellationToken);

        [Route("configuration/remotes")]
        [HttpGet]
        public async Task<IActionResult> RemoteConfig(CancellationToken cancellationToken) => Ok(await configurationApi.GetRemoteConfigs(cancellationToken));
    }
}