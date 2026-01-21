namespace ServiceControl.Infrastructure;

using System;
using Microsoft.Extensions.Logging;
using ServiceControl.Configuration;

/// <summary>
/// Configuration settings for OpenID Connect (OIDC) authentication.
/// When enabled, all API endpoints require a valid JWT Bearer token unless marked with [AllowAnonymous].
/// </summary>
public class OpenIdConnectSettings
{
    readonly ILogger logger = LoggerUtil.CreateStaticLogger<OpenIdConnectSettings>();

    /// <summary>
    /// Initializes OpenID Connect settings from the given configuration root namespace.
    /// </summary>
    /// <param name="rootNamespace"></param>
    /// <param name="validateConfiguration"></param>
    /// <param name="requireServicePulseSettings"></param>
    public OpenIdConnectSettings(SettingsRootNamespace rootNamespace, bool validateConfiguration, bool requireServicePulseSettings = true)
    {
        // Master switch - if disabled, all other authentication settings are ignored
        Enabled = SettingsReader.Read(rootNamespace, "Authentication.Enabled", false);

        // Always read all settings so we can log warnings about ignored configuration
        Authority = SettingsReader.Read<string>(rootNamespace, "Authentication.Authority");
        Audience = SettingsReader.Read<string>(rootNamespace, "Authentication.Audience");
        ValidateIssuer = SettingsReader.Read(rootNamespace, "Authentication.ValidateIssuer", true);
        ValidateAudience = SettingsReader.Read(rootNamespace, "Authentication.ValidateAudience", true);
        ValidateLifetime = SettingsReader.Read(rootNamespace, "Authentication.ValidateLifetime", true);
        ValidateIssuerSigningKey = SettingsReader.Read(rootNamespace, "Authentication.ValidateIssuerSigningKey", true);
        RequireHttpsMetadata = SettingsReader.Read(rootNamespace, "Authentication.RequireHttpsMetadata", true);

        // ServicePulse settings are only relevant for the primary ServiceControl instance
        // which serves the OIDC configuration endpoint that ServicePulse uses for login
        if (requireServicePulseSettings)
        {
            ServicePulseClientId = SettingsReader.Read<string>(rootNamespace, "Authentication.ServicePulse.ClientId");
            ServicePulseApiScopes = SettingsReader.Read<string>(rootNamespace, "Authentication.ServicePulse.ApiScopes");
            ServicePulseAuthority = SettingsReader.Read<string>(rootNamespace, "Authentication.ServicePulse.Authority");
        }

        if (validateConfiguration)
        {
            Validate(requireServicePulseSettings);
        }
    }

    /// <summary>
    /// Master switch for authentication. When false, all other authentication settings are ignored
    /// and all API endpoints are accessible without authentication.
    /// </summary>
    public bool Enabled { get; }

    /// <summary>
    /// The OpenID Connect authority URL (issuer). This is the base URL of the identity provider
    /// that issues tokens (e.g., "https://login.microsoftonline.com/{tenant-id}/v2.0" for Azure AD).
    /// The OIDC discovery document is fetched from {Authority}/.well-known/openid-configuration.
    /// </summary>
    public string Authority { get; }

    /// <summary>
    /// The expected audience claim in the JWT token. Tokens must contain this value in their "aud" claim
    /// to be considered valid. Typically set to the API identifier or application ID.
    /// </summary>
    public string Audience { get; }

    /// <summary>
    /// When true, validates that the token's issuer matches the configured authority.
    /// Disabling this allows tokens from any issuer (security warning logged).
    /// </summary>
    public bool ValidateIssuer { get; }

    /// <summary>
    /// When true, validates that the token's audience matches the configured audience.
    /// Disabling this allows tokens intended for other applications (security warning logged).
    /// </summary>
    public bool ValidateAudience { get; }

    /// <summary>
    /// When true, validates that the token has not expired based on the "exp" claim.
    /// Disabling this allows expired tokens to be accepted (security warning logged).
    /// </summary>
    public bool ValidateLifetime { get; }

    /// <summary>
    /// When true, validates the token's cryptographic signature using keys from the authority's JWKS endpoint.
    /// Disabling this is a serious security risk as it allows forged tokens (security warning logged).
    /// </summary>
    public bool ValidateIssuerSigningKey { get; }

    /// <summary>
    /// When true, requires the authority URL to use HTTPS. Set to false only for local development
    /// with HTTP identity providers (not recommended for production).
    /// </summary>
    public bool RequireHttpsMetadata { get; }

    /// <summary>
    /// Optional override for the authority URL that ServicePulse should use for authentication.
    /// If not specified, ServicePulse uses the main Authority value.
    /// </summary>
    public string ServicePulseAuthority { get; }

    /// <summary>
    /// The OAuth client ID that ServicePulse should use when initiating the authentication flow.
    /// Required on the primary ServiceControl instance when authentication is enabled.
    /// </summary>
    public string ServicePulseClientId { get; }

    /// <summary>
    /// Space-separated list of API scopes that ServicePulse should request during authentication.
    /// Required on the primary ServiceControl instance when authentication is enabled.
    /// </summary>
    public string ServicePulseApiScopes { get; }

    void Validate(bool requireServicePulseSettings)
    {
        if (Enabled)
        {
            ValidateRequiredSettings(requireServicePulseSettings);
        }

        LogConfiguration(requireServicePulseSettings);
    }

    void ValidateRequiredSettings(bool requireServicePulseSettings)
    {
        if (string.IsNullOrWhiteSpace(Authority))
        {
            var message = "Authentication.Authority is required when authentication is enabled. Please provide a valid OpenID Connect authority URL (e.g., https://login.microsoftonline.com/{tenant-id}/v2.0)";
            logger.LogCritical(message);
            throw new Exception(message);
        }

        if (!Uri.TryCreate(Authority, UriKind.Absolute, out var authorityUri))
        {
            var message = $"Authentication.Authority must be a valid absolute URI. Current value: '{Authority}'";
            logger.LogCritical(message);
            throw new Exception(message);
        }

        if (RequireHttpsMetadata && authorityUri.Scheme != Uri.UriSchemeHttps)
        {
            var message = $"Authentication.Authority must use HTTPS when RequireHttpsMetadata is true. Current value: '{Authority}'. Either use HTTPS or set Authentication.RequireHttpsMetadata to false (not recommended for production)";
            logger.LogCritical(message);
            throw new Exception(message);
        }

        if (string.IsNullOrWhiteSpace(Audience))
        {
            var message = "Authentication.Audience is required when authentication is enabled. Please provide a valid audience identifier (typically your API identifier or client ID)";
            logger.LogCritical(message);
            throw new Exception(message);
        }

        if (requireServicePulseSettings)
        {
            if (string.IsNullOrWhiteSpace(ServicePulseClientId))
            {
                var message = "Authentication.ServicePulse.ClientId is required when authentication is enabled. Please provide the OAuth client ID for ServicePulse";
                logger.LogCritical(message);
                throw new Exception(message);
            }

            if (string.IsNullOrWhiteSpace(ServicePulseApiScopes))
            {
                var message = "Authentication.ServicePulse.ApiScopes is required when authentication is enabled. Please provide the API scopes ServicePulse should request";
                logger.LogCritical(message);
                throw new Exception(message);
            }

            if (ServicePulseAuthority != null && !Uri.TryCreate(ServicePulseAuthority, UriKind.Absolute, out _))
            {
                var message = $"Authentication.ServicePulse.Authority must be a valid absolute URI. Current value: '{ServicePulseAuthority}'";
                logger.LogCritical(message);
                throw new Exception(message);
            }
        }
    }

    void LogConfiguration(bool requireServicePulseSettings)
    {
        if (Enabled)
        {
            logger.LogInformation("Authentication is enabled: {@Settings}",
                new
                {
                    Authority,
                    Audience,
                    ValidateIssuer,
                    ValidateAudience,
                    ValidateLifetime,
                    ValidateIssuerSigningKey,
                    RequireHttpsMetadata,
                    ServicePulseClientId = requireServicePulseSettings ? ServicePulseClientId : null,
                    ServicePulseAuthority = requireServicePulseSettings ? ServicePulseAuthority : null,
                    ServicePulseApiScopes = requireServicePulseSettings ? ServicePulseApiScopes : null
                });
        }
        else
        {
            logger.LogInformation("Authentication is disabled: {@Settings}",
                new
                {
                    Authority,
                    Audience,
                    ValidateIssuer,
                    ValidateAudience,
                    ValidateLifetime,
                    ValidateIssuerSigningKey,
                    RequireHttpsMetadata,
                    ServicePulseClientId = requireServicePulseSettings ? ServicePulseClientId : null,
                    ServicePulseAuthority = requireServicePulseSettings ? ServicePulseAuthority : null,
                    ServicePulseApiScopes = requireServicePulseSettings ? ServicePulseApiScopes : null
                });
        }

        // Warn about potential misconfigurations
        var hasAuthConfig = !string.IsNullOrWhiteSpace(Authority) || !string.IsNullOrWhiteSpace(Audience);
        var hasServicePulseConfig = requireServicePulseSettings &&
            (!string.IsNullOrWhiteSpace(ServicePulseClientId) || !string.IsNullOrWhiteSpace(ServicePulseApiScopes) || !string.IsNullOrWhiteSpace(ServicePulseAuthority));

        if (!Enabled && (hasAuthConfig || hasServicePulseConfig))
        {
            logger.LogWarning("Authentication is disabled but authentication settings are configured. These settings will be ignored");
        }

        if (Enabled && !ValidateIssuer)
        {
            logger.LogWarning("Authentication.ValidateIssuer is disabled. Tokens from any issuer will be accepted. Its recommended to keep this enabled for security");
        }

        if (Enabled && !ValidateAudience)
        {
            logger.LogWarning("Authentication.ValidateAudience is disabled. Tokens intended for other applications will be accepted. Its recommended to keep this enabled for security");
        }

        if (Enabled && !ValidateLifetime)
        {
            logger.LogWarning("Authentication.ValidateLifetime is disabled. Expired tokens will be accepted. Its recommended to keep this enabled for security");
        }

        if (Enabled && !ValidateIssuerSigningKey)
        {
            logger.LogWarning("Authentication.ValidateIssuerSigningKey is disabled. Forged tokens may be accepted. Its recommended to keep this enabled for security");
        }

        if (Enabled && !RequireHttpsMetadata)
        {
            logger.LogWarning("Authentication.RequireHttpsMetadata is disabled. OIDC metadata will be fetched over HTTP which is insecure. Its recommended to keep this enabled for security");
        }
    }
}
