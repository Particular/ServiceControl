namespace ServiceControl.Infrastructure;

using System;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using ServiceControl.Configuration;

public class OpenIdConnectSettings
{
    readonly ILogger logger = LoggerUtil.CreateStaticLogger<OpenIdConnectSettings>();

    public OpenIdConnectSettings(SettingsRootNamespace rootNamespace, bool validateConfiguration, bool requireServicePulseSettings = true)
    {
        Enabled = SettingsReader.Read(rootNamespace, "Authentication.Enabled", false);

        if (!Enabled)
        {
            return;
        }

        Authority = SettingsReader.Read<string>(rootNamespace, "Authentication.Authority");
        Audience = SettingsReader.Read<string>(rootNamespace, "Authentication.Audience");
        ValidateIssuer = SettingsReader.Read(rootNamespace, "Authentication.ValidateIssuer", true);
        ValidateAudience = SettingsReader.Read(rootNamespace, "Authentication.ValidateAudience", true);
        ValidateLifetime = SettingsReader.Read(rootNamespace, "Authentication.ValidateLifetime", true);
        ValidateIssuerSigningKey = SettingsReader.Read(rootNamespace, "Authentication.ValidateIssuerSigningKey", true);
        RequireHttpsMetadata = SettingsReader.Read(rootNamespace, "Authentication.RequireHttpsMetadata", true);

        // ServicePulse settings are only needed for the primary ServiceControl instance
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

    [JsonPropertyName("enabled")]
    public bool Enabled { get; }

    [JsonPropertyName("authority")]
    public string Authority { get; }

    [JsonPropertyName("audience")]
    public string Audience { get; }

    [JsonPropertyName("validateIssuer")]
    public bool ValidateIssuer { get; }

    [JsonPropertyName("validateAudience")]
    public bool ValidateAudience { get; }

    [JsonPropertyName("validateLifetime")]
    public bool ValidateLifetime { get; }

    [JsonPropertyName("validateIssuerSigningKey")]
    public bool ValidateIssuerSigningKey { get; }

    [JsonPropertyName("requireHttpsMetadata")]
    public bool RequireHttpsMetadata { get; }

    [JsonPropertyName("servicePulseAuthority")]
    public string ServicePulseAuthority { get; }

    [JsonPropertyName("servicePulseClientId")]
    public string ServicePulseClientId { get; }

    [JsonPropertyName("servicePulseApiScopes")]
    public string ServicePulseApiScopes { get; }

    void Validate(bool requireServicePulseSettings)
    {
        if (!Enabled)
        {
            return;
        }

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
            logger.LogWarning("Authentication.ValidateIssuer is set to false. This is not recommended for production environments as it may allow tokens from untrusted issuers");
        }

        if (!ValidateAudience)
        {
            logger.LogWarning("Authentication.ValidateAudience is set to false. This is not recommended for production environments as it may allow tokens intended for other applications");
        }

        if (!ValidateLifetime)
        {
            logger.LogWarning("Authentication.ValidateLifetime is set to false. This is not recommended as it may allow expired tokens to be accepted");
        }

        if (!ValidateIssuerSigningKey)
        {
            logger.LogWarning("Authentication.ValidateIssuerSigningKey is set to false. This is a serious security risk and should only be used in development environments");
        }

        // ServicePulse settings are only required for the primary ServiceControl instance
        if (requireServicePulseSettings)
        {
            if (string.IsNullOrWhiteSpace(ServicePulseClientId))
            {
                throw new Exception("Authentication.ServicePulse.ClientId is required when authentication is enabled on the primary ServiceControl instance.");
            }

            if (string.IsNullOrWhiteSpace(ServicePulseApiScopes))
            {
                throw new Exception("Authentication.ServicePulse.ApiScopes is required when authentication is enabled on the primary ServiceControl instance.");
            }

            if (ServicePulseAuthority != null && !Uri.TryCreate(ServicePulseAuthority, UriKind.Absolute, out _))
            {
                throw new Exception("Authentication.ServicePulse.Authority must be a valid absolute URI if provided.");
            }
        }

        logger.LogInformation("Authentication configuration validated successfully");
        logger.LogInformation("  Authority: {Authority}", Authority);
        logger.LogInformation("  Audience: {Audience}", Audience);
        logger.LogInformation("  ValidateIssuer: {ValidateIssuer}", ValidateIssuer);
        logger.LogInformation("  ValidateAudience: {ValidateAudience}", ValidateAudience);
        logger.LogInformation("  ValidateLifetime: {ValidateLifetime}", ValidateLifetime);
        logger.LogInformation("  ValidateIssuerSigningKey: {ValidateIssuerSigningKey}", ValidateIssuerSigningKey);
        logger.LogInformation("  RequireHttpsMetadata: {RequireHttpsMetadata}", RequireHttpsMetadata);

        if (requireServicePulseSettings)
        {
            logger.LogInformation("  ServicePulseClientId: {ServicePulseClientId}", ServicePulseClientId);
            logger.LogInformation("  ServicePulseAuthority: {ServicePulseAuthority}", ServicePulseAuthority);
            logger.LogInformation("  ServicePulseApiScopes: {ServicePulseApiScopes}", ServicePulseApiScopes);
        }
    }
}
