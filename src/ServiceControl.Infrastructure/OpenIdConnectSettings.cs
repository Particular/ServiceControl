namespace ServiceControl.Infrastructure;

using System;
using System.Text.Json.Serialization;
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
    /// Initializes OpenID Connect settings by reading configuration values from the SettingsReader.
    /// </summary>
    /// <param name="rootNamespace">The settings root namespace (e.g., "ServiceControl", "ServiceControl.Audit").</param>
    /// <param name="validateConfiguration">
    /// When true, validates that all required settings are present and logs security warnings
    /// for any disabled validation flags. Throws an exception if required settings are missing.
    /// </param>
    /// <param name="requireServicePulseSettings">
    /// When true (default), requires ServicePulse-specific settings (ClientId, ApiScopes).
    /// Set to false for Audit and Monitoring instances which don't serve the ServicePulse UI.
    /// </param>
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
    [JsonPropertyName("enabled")]
    public bool Enabled { get; }

    /// <summary>
    /// The OpenID Connect authority URL (issuer). This is the base URL of the identity provider
    /// that issues tokens (e.g., "https://login.microsoftonline.com/{tenant-id}/v2.0" for Azure AD).
    /// The OIDC discovery document is fetched from {Authority}/.well-known/openid-configuration.
    /// </summary>
    [JsonPropertyName("authority")]
    public string Authority { get; }

    /// <summary>
    /// The expected audience claim in the JWT token. Tokens must contain this value in their "aud" claim
    /// to be considered valid. Typically set to the API identifier or application ID.
    /// </summary>
    [JsonPropertyName("audience")]
    public string Audience { get; }

    /// <summary>
    /// When true, validates that the token's issuer matches the configured authority.
    /// Disabling this allows tokens from any issuer (security warning logged).
    /// </summary>
    [JsonPropertyName("validateIssuer")]
    public bool ValidateIssuer { get; }

    /// <summary>
    /// When true, validates that the token's audience matches the configured audience.
    /// Disabling this allows tokens intended for other applications (security warning logged).
    /// </summary>
    [JsonPropertyName("validateAudience")]
    public bool ValidateAudience { get; }

    /// <summary>
    /// When true, validates that the token has not expired based on the "exp" claim.
    /// Disabling this allows expired tokens to be accepted (security warning logged).
    /// </summary>
    [JsonPropertyName("validateLifetime")]
    public bool ValidateLifetime { get; }

    /// <summary>
    /// When true, validates the token's cryptographic signature using keys from the authority's JWKS endpoint.
    /// Disabling this is a serious security risk as it allows forged tokens (security warning logged).
    /// </summary>
    [JsonPropertyName("validateIssuerSigningKey")]
    public bool ValidateIssuerSigningKey { get; }

    /// <summary>
    /// When true, requires the authority URL to use HTTPS. Set to false only for local development
    /// with HTTP identity providers (not recommended for production).
    /// </summary>
    [JsonPropertyName("requireHttpsMetadata")]
    public bool RequireHttpsMetadata { get; }

    /// <summary>
    /// Optional override for the authority URL that ServicePulse should use for authentication.
    /// If not specified, ServicePulse uses the main Authority value.
    /// </summary>
    [JsonPropertyName("servicePulseAuthority")]
    public string ServicePulseAuthority { get; }

    /// <summary>
    /// The OAuth client ID that ServicePulse should use when initiating the authentication flow.
    /// Required on the primary ServiceControl instance when authentication is enabled.
    /// </summary>
    [JsonPropertyName("servicePulseClientId")]
    public string ServicePulseClientId { get; }

    /// <summary>
    /// Space-separated list of API scopes that ServicePulse should request during authentication.
    /// Required on the primary ServiceControl instance when authentication is enabled.
    /// </summary>
    [JsonPropertyName("servicePulseApiScopes")]
    public string ServicePulseApiScopes { get; }

    /// <summary>
    /// Validates the authentication configuration, ensuring required settings are present
    /// and logging warnings for any security-related settings that are disabled.
    /// </summary>
    /// <param name="requireServicePulseSettings">
    /// When true, also validates that ServicePulse settings (ClientId, ApiScopes) are provided.
    /// </param>
    /// <exception cref="Exception">Thrown when required settings are missing or invalid.</exception>
    void Validate(bool requireServicePulseSettings)
    {
        if (!Enabled)
        {
            LogDisabledConfiguration(requireServicePulseSettings);
            return;
        }

        ValidateEnabledConfiguration(requireServicePulseSettings);

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

    void LogDisabledConfiguration(bool requireServicePulseSettings)
    {
        // Check if any settings are configured but will be ignored because auth is disabled
        var hasIgnoredSettings =
            !string.IsNullOrWhiteSpace(Authority) ||
            !string.IsNullOrWhiteSpace(Audience) ||
            !ValidateIssuer ||
            !ValidateAudience ||
            !ValidateLifetime ||
            !ValidateIssuerSigningKey ||
            !RequireHttpsMetadata ||
            (requireServicePulseSettings && !string.IsNullOrWhiteSpace(ServicePulseClientId)) ||
            (requireServicePulseSettings && !string.IsNullOrWhiteSpace(ServicePulseApiScopes)) ||
            (requireServicePulseSettings && !string.IsNullOrWhiteSpace(ServicePulseAuthority));

        if (hasIgnoredSettings)
        {
            logger.LogWarning("Authentication is disabled but authentication settings are configured. These settings will be ignored: {@Settings}",
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
            logger.LogInformation("Authentication is disabled");
        }
    }

    void ValidateEnabledConfiguration(bool requireServicePulseSettings)
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

        if (!ValidateIssuer)
        {
            logger.LogWarning("Authentication.ValidateIssuer is set to false. This is not recommended for production environments as it allows tokens from untrusted issuers");
        }

        if (!ValidateAudience)
        {
            logger.LogWarning("Authentication.ValidateAudience is set to false. This is not recommended for production environments as it allows tokens intended for other applications");
        }

        if (!ValidateLifetime)
        {
            logger.LogWarning("Authentication.ValidateLifetime is set to false. This is not recommended for production environments as it allows expired tokens to be accepted");
        }

        if (!ValidateIssuerSigningKey)
        {
            logger.LogWarning("Authentication.ValidateIssuerSigningKey is set to false. This is not recommended for production environments as it allows forged tokens to be accepted");
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
}
