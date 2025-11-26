namespace ServiceControl.Authentication
{
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using ServiceBus.Management.Infrastructure.Settings;

    [ApiController]
    [Route("api/authentication")]
    public class AuthenticationController(Settings settings) : ControllerBase
    {
        [HttpGet]
        [AllowAnonymous]
        [Route("configuration")]
        public ActionResult<AuthConfig> Configuration()
        {
            var info = new AuthConfig
            {
                Enabled = settings.OpenIdConnectSettings.ServicePulseEnabled,
                ClientId = settings.OpenIdConnectSettings.ServicePulseClientId,
                Authority = settings.OpenIdConnectSettings.ServicePulseAuthority,
                ApiScope = settings.OpenIdConnectSettings.ServicePulseApiScope
            };

            return Ok(info);
        }
    }

    public class AuthConfig
    {
        public bool Enabled { get; set; }
        public string ClientId { get; set; }
        public string Authority { get; set; }
        public string ApiScope { get; set; }
    }
}
