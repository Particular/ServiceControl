namespace ServiceControl.Infrastructure.WebApi
{
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
        public RootUrls Urls() => configurationApi.GetUrls(Request.GetDisplayUrl() + "/");

        [Route("instance-info")]
        [Route("configuration")]
        [HttpGet]
        public object Config() => configurationApi.GetConfig();

        [Route("configuration/remotes")]
        [HttpGet]
        public async Task<IActionResult> RemoteConfig() => Ok(await configurationApi.GetRemoteConfigs());
    }
}