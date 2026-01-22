namespace ServiceControl.Infrastructure;

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using ServiceControl.Configuration;

public class CorsSettings
{
    readonly ILogger logger = LoggerUtil.CreateStaticLogger<CorsSettings>();

    /// <summary>
    /// Initializes CORS settings from the given configuration root namespace.
    /// </summary>
    /// <param name="rootNamespace"></param>
    public CorsSettings(SettingsRootNamespace rootNamespace)
    {
        // Default to allowing any origin for backwards compatibility
        AllowAnyOrigin = SettingsReader.Read(rootNamespace, "Cors.AllowAnyOrigin", true);

        var allowedOriginsValue = SettingsReader.Read<string>(rootNamespace, "Cors.AllowedOrigins");
        if (!string.IsNullOrWhiteSpace(allowedOriginsValue))
        {
            AllowedOrigins = ParseOrigins(allowedOriginsValue);

            // If specific origins are configured, disable AllowAnyOrigin
            if (AllowedOrigins.Count > 0 && AllowAnyOrigin)
            {
                logger.LogInformation("Cors.AllowedOrigins configured, setting AllowAnyOrigin to false");
                AllowAnyOrigin = false;
            }
        }

        LogConfiguration();
    }

    /// <summary>
    /// When true, allows requests from any origin. Default is true for backwards compatibility.
    /// </summary>
    public bool AllowAnyOrigin { get; private set; }

    /// <summary>
    /// List of specific origins to allow when AllowAnyOrigin is false.
    /// </summary>
    public IReadOnlyList<string> AllowedOrigins { get; } = [];

    List<string> ParseOrigins(string value)
    {
        var origins = new List<string>();
        var parts = value.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var part in parts)
        {
            if (Uri.TryCreate(part, UriKind.Absolute, out var uri))
            {
                // Normalize: use origin format (scheme://host:port)
                var origin = $"{uri.Scheme}://{uri.Authority}";
                origins.Add(origin);
            }
            else
            {
                logger.LogWarning("Invalid origin URL in Cors.AllowedOrigins: '{InvalidOrigin}'", part);
            }
        }

        return origins;
    }

    void LogConfiguration()
    {
        var allowedOriginsDisplay = AllowedOrigins.Count > 0 ? string.Join(", ", AllowedOrigins) : "(none)";

        logger.LogInformation("CORS configuration: AllowAnyOrigin={AllowAnyOrigin}, AllowedOrigins={AllowedOrigins}",
            AllowAnyOrigin, allowedOriginsDisplay);

        // Warn about potential misconfigurations
        if (AllowAnyOrigin)
        {
            logger.LogWarning("CORS is configured to allow any origin. Consider configuring Cors.AllowedOrigins for production environments");
        }

        if (!AllowAnyOrigin && AllowedOrigins.Count == 0)
        {
            logger.LogWarning("CORS has no origins configured and AllowAnyOrigin is false. All cross-origin requests will be blocked");
        }
    }
}
