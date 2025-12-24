namespace ServiceControl.Infrastructure;

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using ServiceControl.Configuration;

public class CorsSettings
{
    readonly ILogger logger = LoggerUtil.CreateStaticLogger<CorsSettings>();

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
    public List<string> AllowedOrigins { get; } = [];

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
        logger.LogInformation("CORS configuration:");
        logger.LogInformation("  AllowAnyOrigin: {AllowAnyOrigin}", AllowAnyOrigin);

        if (AllowedOrigins.Count > 0)
        {
            logger.LogInformation("  AllowedOrigins: {AllowedOrigins}", string.Join(", ", AllowedOrigins));
        }

        if (AllowAnyOrigin)
        {
            logger.LogWarning("Cors.AllowAnyOrigin is true. Any website can make requests to this API. Consider configuring Cors.AllowedOrigins for production environments.");
        }
    }
}
