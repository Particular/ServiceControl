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
        public Task<RootUrls> Urls() => configurationApi.GetUrls(Request.GetDisplayUrl(), default);

        [Route("instance-info")]
        [Route("configuration")]
        [HttpGet]
        public Task<object> Config() => configurationApi.GetConfig(default);

        [Route("configuration/remotes")]
        [HttpGet]
        public async Task<IActionResult> RemoteConfig(CancellationToken cancellationToken) =>
            Ok(await configurationApi.GetRemoteConfigs(cancellationToken));
    }
}