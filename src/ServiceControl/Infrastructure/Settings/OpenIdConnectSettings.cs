namespace ServiceBus.Management.Infrastructure.Settings
{
    using System;
    using System.Text.Json.Serialization;
    using Microsoft.Extensions.Logging;
    using ServiceControl.Configuration;
    using ServiceControl.Infrastructure;

    public class OpenIdConnectSettings
    {
        readonly ILogger logger = LoggerUtil.CreateStaticLogger<OpenIdConnectSettings>();

        public OpenIdConnectSettings(bool validateConfiguration)
        {
            Enabled = SettingsReader.Read(Settings.SettingsRootNamespace, "Authentication.Enabled", false);

            if (!Enabled)
            {
                return;
            }

            Authority = SettingsReader.Read<string>(Settings.SettingsRootNamespace, "Authentication.Authority");
            Audience = SettingsReader.Read<string>(Settings.SettingsRootNamespace, "Authentication.Audience");
            ValidateIssuer = SettingsReader.Read(Settings.SettingsRootNamespace, "Authentication.ValidateIssuer", true);
            ValidateAudience = SettingsReader.Read(Settings.SettingsRootNamespace, "Authentication.ValidateAudience", true);
            ValidateLifetime = SettingsReader.Read(Settings.SettingsRootNamespace, "Authentication.ValidateLifetime", true);
            ValidateIssuerSigningKey = SettingsReader.Read(Settings.SettingsRootNamespace, "Authentication.ValidateIssuerSigningKey", true);
            RequireHttpsMetadata = SettingsReader.Read(Settings.SettingsRootNamespace, "Authentication.RequireHttpsMetadata", true);

            ServicePulseEnabled = SettingsReader.Read(Settings.SettingsRootNamespace, "Authentication.ServicePulse.Enabled", false);

            if (ServicePulseEnabled)
            {
                ServicePulseClientId = SettingsReader.Read<string>(Settings.SettingsRootNamespace, "Authentication.ServicePulse.ClientId");
                ServicePulseApiScope = SettingsReader.Read<string>(Settings.SettingsRootNamespace, "Authentication.ServicePulse.ApiScope");
                ServicePulseAuthority = SettingsReader.Read<string>(Settings.SettingsRootNamespace, "Authentication.ServicePulse.Authority");
            }

            if (validateConfiguration)
            {
                Validate();
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

        [JsonPropertyName("servicePulseEnabled")]
        public bool ServicePulseEnabled { get; }

        [JsonPropertyName("servicePulseAuthority")]
        public string ServicePulseAuthority { get; }

        [JsonPropertyName("servicePulseClientId")]
        public string ServicePulseClientId { get; }

        [JsonPropertyName("servicePulseApiScope")]
        public string ServicePulseApiScope { get; }

        void Validate()
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

            if (ServicePulseEnabled)
            {
                if (string.IsNullOrWhiteSpace(ServicePulseClientId))
                {
                    throw new Exception("Authentication.ServicePulse.ClientId is required when Authentication.ServicePulse.Enabled is true.");
                }

                if (string.IsNullOrWhiteSpace(ServicePulseApiScope))
                {
                    throw new Exception("Authentication.ServicePulse.ApiScope is required when Authentication.ServicePulse.Enabled is true.");
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
            logger.LogInformation("  ServicePulseEnabled: {ServicePulseEnabled}", ServicePulseEnabled);
            logger.LogInformation("  ServicePulseClientId: {ServicePulseClientId}", ServicePulseClientId);
            logger.LogInformation("  ServicePulseAuthority: {ServicePulseAuthority}", ServicePulseAuthority);
            logger.LogInformation("  ServicePulseApiScope: {ServicePulseApiScope}", ServicePulseApiScope);
        }
    }
}
