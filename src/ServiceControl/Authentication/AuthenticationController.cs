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
        // Must be anonymous so unauthenticated clients can discover auth configuration before authenticating
        [AllowAnonymous]
        [Route("configuration")]
        public ActionResult<AuthConfig> Configuration()
        {
            var info = new AuthConfig
            {
                Enabled = settings.OpenIdConnectSettings.Enabled,
                ClientId = settings.OpenIdConnectSettings.ServicePulseClientId,
                Authority = settings.OpenIdConnectSettings.ServicePulseAuthority,
                Audience = settings.OpenIdConnectSettings.Audience,
                ApiScopes = settings.OpenIdConnectSettings.ServicePulseApiScopes
            };

            return Ok(info);
        }
    }

    /// <summary>
    /// Authentication configuration information exposed via the API.
    /// This will be serialized to JSON using snake_case naming.
    /// </summary>
    public class AuthConfig
    {
        public bool Enabled { get; set; }
        public string ClientId { get; set; }
        public string Authority { get; set; }
        public string Audience { get; set; }
        public string ApiScopes { get; set; }
    }
}
