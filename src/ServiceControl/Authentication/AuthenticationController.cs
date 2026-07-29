namespace ServiceControl.Authentication
{
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using ServiceBus.Management.Infrastructure.Settings;

    [ApiController]
    [Route("api/authentication")]
    public class AuthenticationController(Settings settings) : ControllerBase
    {
        // Must be anonymous so unauthenticated clients can discover auth configuration before authenticating
        [AllowAnonymous]
        [HttpGet]
        [Route("configuration")]
        public ActionResult<AuthConfig> Configuration()
        {
            var info = new AuthConfig
            {
                Enabled = settings.OpenIdConnectSettings.Enabled,
                RoleBasedAuthorizationEnabled = settings.OpenIdConnectSettings.RoleBasedAuthorizationEnabled,
                ClientId = settings.OpenIdConnectSettings.ServicePulseClientId,
                Authority = settings.OpenIdConnectSettings.ServicePulseAuthority,
                Audience = settings.OpenIdConnectSettings.Audience,
                ApiScopes = settings.OpenIdConnectSettings.ServicePulseApiScopes,
                Scopes = settings.OpenIdConnectSettings.ServicePulseScopes
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
        public bool RoleBasedAuthorizationEnabled { get; set; }
        public string ClientId { get; set; }
        public string Authority { get; set; }
        public string Audience { get; set; }
        public string ApiScopes { get; set; }

        /// <summary>
        /// The complete, space-separated scope string ServicePulse should request. Carries the value of
        /// <c>OpenIdConnectSettings.ServicePulseScopes</c> (see there for how it is composed). Added as an
        /// additive, non-breaking field; ServicePulse builds that don't recognize it fall back to
        /// composing the scope string themselves from <see cref="ApiScopes"/>.
        /// </summary>
        public string Scopes { get; set; }
    }
}
