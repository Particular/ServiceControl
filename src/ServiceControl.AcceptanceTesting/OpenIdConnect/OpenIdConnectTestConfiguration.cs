namespace ServiceControl.AcceptanceTesting.OpenIdConnect
{
    using System;

    /// <summary>
    /// Helper class to configure OpenID Connect environment variables for acceptance tests.
    /// Environment variables must be set before the ServiceControl instance starts.
    /// </summary>
    /// <remarks>
    /// Creates a new OpenID Connect test configuration.
    /// </remarks>
    /// <param name="instanceType">The instance type (determines environment variable prefix)</param>
    public class OpenIdConnectTestConfiguration(ServiceControlInstanceType instanceType) : IDisposable
    {
        readonly string envVarPrefix = EnvironmentVariablePrefixes.GetPrefix(instanceType);
        bool disposed;

        /// <summary>
        /// Enables OpenID Connect authentication.
        /// When enabled, all API endpoints require a valid JWT Bearer token unless marked with [AllowAnonymous].
        /// </summary>
        public OpenIdConnectTestConfiguration WithAuthenticationEnabled()
        {
            SetEnvironmentVariable("AUTHENTICATION_ENABLED", "true");
            return this;
        }

        /// <summary>
        /// Disables OpenID Connect authentication (default behavior).
        /// </summary>
        public OpenIdConnectTestConfiguration WithAuthenticationDisabled()
        {
            SetEnvironmentVariable("AUTHENTICATION_ENABLED", "false");
            return this;
        }

        /// <summary>
        /// Disables settings validation. This allows testing with placeholder/fake OIDC settings.
        /// Should only be used in test scenarios where a real OIDC provider is not available.
        /// </summary>
        public OpenIdConnectTestConfiguration WithConfigurationValidationDisabled()
        {
            SetEnvironmentVariable("VALIDATECONFIG", "false");
            return this;
        }

        /// <summary>
        /// Configures the OpenID Connect authority URL (issuer).
        /// </summary>
        /// <param name="authority">The authority URL (e.g., https://login.microsoftonline.com/{tenant-id}/v2.0)</param>
        public OpenIdConnectTestConfiguration WithAuthority(string authority)
        {
            SetEnvironmentVariable("AUTHENTICATION_AUTHORITY", authority);
            return this;
        }

        /// <summary>
        /// Configures the expected audience claim in the JWT token.
        /// </summary>
        /// <param name="audience">The audience identifier</param>
        public OpenIdConnectTestConfiguration WithAudience(string audience)
        {
            SetEnvironmentVariable("AUTHENTICATION_AUDIENCE", audience);
            return this;
        }

        /// <summary>
        /// Configures whether to validate the token's issuer.
        /// Default is true. Set to false only for testing purposes.
        /// </summary>
        public OpenIdConnectTestConfiguration WithValidateIssuer(bool validate)
        {
            SetEnvironmentVariable("AUTHENTICATION_VALIDATEISSUER", validate.ToString().ToLowerInvariant());
            return this;
        }

        /// <summary>
        /// Configures whether to validate the token's audience.
        /// Default is true. Set to false only for testing purposes.
        /// </summary>
        public OpenIdConnectTestConfiguration WithValidateAudience(bool validate)
        {
            SetEnvironmentVariable("AUTHENTICATION_VALIDATEAUDIENCE", validate.ToString().ToLowerInvariant());
            return this;
        }

        /// <summary>
        /// Configures whether to validate the token's lifetime.
        /// Default is true. Set to false only for testing purposes.
        /// </summary>
        public OpenIdConnectTestConfiguration WithValidateLifetime(bool validate)
        {
            SetEnvironmentVariable("AUTHENTICATION_VALIDATELIFETIME", validate.ToString().ToLowerInvariant());
            return this;
        }

        /// <summary>
        /// Configures whether to validate the token's signing key.
        /// Default is true. Set to false only for testing purposes.
        /// </summary>
        public OpenIdConnectTestConfiguration WithValidateIssuerSigningKey(bool validate)
        {
            SetEnvironmentVariable("AUTHENTICATION_VALIDATEISSUERSIGNINGKEY", validate.ToString().ToLowerInvariant());
            return this;
        }

        /// <summary>
        /// Configures whether to require HTTPS for metadata retrieval.
        /// Default is true. Set to false for local development with HTTP identity providers.
        /// </summary>
        public OpenIdConnectTestConfiguration WithRequireHttpsMetadata(bool require)
        {
            SetEnvironmentVariable("AUTHENTICATION_REQUIREHTTPSMETADATA", require.ToString().ToLowerInvariant());
            return this;
        }

        /// <summary>
        /// Configures the OAuth client ID that ServicePulse should use.
        /// Required on the primary ServiceControl instance when authentication is enabled.
        /// </summary>
        /// <param name="clientId">The client ID</param>
        public OpenIdConnectTestConfiguration WithServicePulseClientId(string clientId)
        {
            SetEnvironmentVariable("AUTHENTICATION_SERVICEPULSE_CLIENTID", clientId);
            return this;
        }

        /// <summary>
        /// Configures the API scopes that ServicePulse should request.
        /// Required on the primary ServiceControl instance when authentication is enabled.
        /// </summary>
        /// <param name="scopes">Space-separated list of API scopes</param>
        public OpenIdConnectTestConfiguration WithServicePulseApiScopes(string scopes)
        {
            SetEnvironmentVariable("AUTHENTICATION_SERVICEPULSE_APISCOPES", scopes);
            return this;
        }

        /// <summary>
        /// Configures an optional override for the authority URL that ServicePulse should use.
        /// If not specified, ServicePulse uses the main Authority value.
        /// </summary>
        /// <param name="authority">The ServicePulse authority URL</param>
        public OpenIdConnectTestConfiguration WithServicePulseAuthority(string authority)
        {
            SetEnvironmentVariable("AUTHENTICATION_SERVICEPULSE_AUTHORITY", authority);
            return this;
        }

        /// <summary>
        /// Clears all OpenID Connect environment variables.
        /// Called automatically on Dispose.
        /// </summary>
        public void ClearConfiguration()
        {
            ClearEnvironmentVariable("AUTHENTICATION_ENABLED");
            ClearEnvironmentVariable("AUTHENTICATION_AUTHORITY");
            ClearEnvironmentVariable("AUTHENTICATION_AUDIENCE");
            ClearEnvironmentVariable("AUTHENTICATION_VALIDATEISSUER");
            ClearEnvironmentVariable("AUTHENTICATION_VALIDATEAUDIENCE");
            ClearEnvironmentVariable("AUTHENTICATION_VALIDATELIFETIME");
            ClearEnvironmentVariable("AUTHENTICATION_VALIDATEISSUERSIGNINGKEY");
            ClearEnvironmentVariable("AUTHENTICATION_REQUIREHTTPSMETADATA");
            ClearEnvironmentVariable("AUTHENTICATION_SERVICEPULSE_CLIENTID");
            ClearEnvironmentVariable("AUTHENTICATION_SERVICEPULSE_APISCOPES");
            ClearEnvironmentVariable("AUTHENTICATION_SERVICEPULSE_AUTHORITY");
            ClearEnvironmentVariable("VALIDATECONFIG");
        }

        void SetEnvironmentVariable(string name, string value) => Environment.SetEnvironmentVariable(envVarPrefix + name, value);

        void ClearEnvironmentVariable(string name) => Environment.SetEnvironmentVariable(envVarPrefix + name, null);

        public void Dispose()
        {
            if (!disposed)
            {
                ClearConfiguration();
                disposed = true;
            }
        }
    }
}
