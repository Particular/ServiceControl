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
        /// The complete, space-separated scope string ServicePulse should request, composed by
        /// ServiceControl from <see cref="ApiScopes"/> plus the fixed <c>openid profile email</c>
        /// scopes and <c>offline_access</c> unless disabled via
        /// <c>Authentication.ServicePulse.OfflineAccessScopeEnabled</c>. Older ServicePulse builds
        /// that predate this field ignore it and fall back to assembling their own scope string.
        /// </summary>
        public string Scopes { get; set; }
    }
}
