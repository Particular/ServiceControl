namespace ServiceControl.Infrastructure;

using System;
using System.Collections.Generic;
using System.Net;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using ServiceControl.Configuration;

public class ForwardedHeadersSettings
{
    readonly ILogger logger = LoggerUtil.CreateStaticLogger<ForwardedHeadersSettings>();

    /// <summary>
    /// Initializes forwarded headers settings from the given configuration root namespace.
    /// </summary>
    /// <param name="rootNamespace"></param>
    public ForwardedHeadersSettings(SettingsRootNamespace rootNamespace)
    {
        Enabled = SettingsReader.Read(rootNamespace, "ForwardedHeaders.Enabled", true);

        // Default to trusting all proxies for backwards compatibility
        // Customers can set this to false and configure KnownProxies/KnownNetworks for better security
        TrustAllProxies = SettingsReader.Read(rootNamespace, "ForwardedHeaders.TrustAllProxies", true);

        var knownProxiesValue = SettingsReader.Read<string>(rootNamespace, "ForwardedHeaders.KnownProxies");
        if (!string.IsNullOrWhiteSpace(knownProxiesValue))
        {
            KnownProxiesRaw = ParseAndValidateIPAddresses(knownProxiesValue);
        }

        var knownNetworksValue = SettingsReader.Read<string>(rootNamespace, "ForwardedHeaders.KnownNetworks");
        if (!string.IsNullOrWhiteSpace(knownNetworksValue))
        {
            KnownNetworks = ParseNetworks(knownNetworksValue);
        }

        // If proxies or networks are explicitly configured, disable TrustAllProxies
        if ((KnownProxiesRaw.Count > 0 || KnownNetworks.Count > 0) && TrustAllProxies)
        {
            logger.LogInformation("KnownProxies or KnownNetworks configured, setting TrustAllProxies to false");
            TrustAllProxies = false;
        }

        LogConfiguration();
    }

    /// <summary>
    /// When true, forwarded headers processing is enabled.
    /// </summary>
    public bool Enabled { get; }

    /// <summary>
    /// When true, all proxies are trusted and X-Forwarded-* headers are processed from any client.
    /// When false, only proxies/networks listed in KnownProxies/KnownNetworks are trusted.
    /// </summary>
    public bool TrustAllProxies { get; private set; }

    // Store as strings for serialization compatibility, parse to IPAddress when needed
    /// <summary>
    /// List of specific IP addresses of trusted proxies.
    /// </summary>
    public List<string> KnownProxiesRaw { get; } = [];

    /// <summary>
    /// List of specific CIDR networks of trusted proxies.
    /// </summary>
    public List<string> KnownNetworks { get; } = [];

    // Parse IPAddresses on demand to avoid serialization issues
    [JsonIgnore]
    public IEnumerable<IPAddress> KnownProxies
    {
        get
        {
            foreach (var raw in KnownProxiesRaw)
            {
                if (IPAddress.TryParse(raw, out var address))
                {
                    yield return address;
                }
            }
        }
    }

    List<string> ParseAndValidateIPAddresses(string value)
    {
        var addresses = new List<string>();
        var parts = value.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var part in parts)
        {
            if (IPAddress.TryParse(part, out _))
            {
                addresses.Add(part);
            }
            else
            {
                logger.LogWarning("Invalid IP address in ForwardedHeaders.KnownProxies: '{InvalidAddress}'", part);
            }
        }

        return addresses;
    }

    List<string> ParseNetworks(string value)
    {
        var networks = new List<string>();
        var parts = value.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var part in parts)
        {
            // Basic validation - should contain a /
            if (part.Contains('/'))
            {
                networks.Add(part);
            }
            else
            {
                logger.LogWarning("Invalid network CIDR in ForwardedHeaders.KnownNetworks (expected format: '10.0.0.0/8'): '{InvalidNetwork}'", part);
            }
        }

        return networks;
    }

    void LogConfiguration()
    {
        var hasProxyConfig = KnownProxiesRaw.Count > 0 || KnownNetworks.Count > 0;
        var knownProxiesDisplay = KnownProxiesRaw.Count > 0 ? string.Join(", ", KnownProxiesRaw) : "(none)";
        var knownNetworksDisplay = KnownNetworks.Count > 0 ? string.Join(", ", KnownNetworks) : "(none)";

        if (Enabled)
        {
            logger.LogInformation("Forwarded headers processing is enabled: Enabled={Enabled}, TrustAllProxies={TrustAllProxies}, KnownProxies={KnownProxies}, KnownNetworks={KnownNetworks}",
                Enabled, TrustAllProxies, knownProxiesDisplay, knownNetworksDisplay);
        }
        else
        {
            logger.LogInformation("Forwarded headers processing is disabled: Enabled={Enabled}, TrustAllProxies={TrustAllProxies}, KnownProxies={KnownProxies}, KnownNetworks={KnownNetworks}",
                Enabled, TrustAllProxies, knownProxiesDisplay, knownNetworksDisplay);
        }

        // Warn about potential misconfigurations
        if (!Enabled && hasProxyConfig)
        {
            logger.LogWarning("Forwarded headers processing is disabled but proxy configuration is present. KnownProxies and KnownNetworks settings will be ignored");
        }

        if (Enabled && TrustAllProxies)
        {
            logger.LogWarning("Forwarded headers is configured to trust all proxies. Any client can spoof X-Forwarded-* headers. Consider configuring KnownProxies or KnownNetworks for production environments");
        }

        if (Enabled && !TrustAllProxies && !hasProxyConfig)
        {
            logger.LogWarning("Forwarded headers is enabled but no trusted proxies are configured. X-Forwarded-* headers will not be processed");
        }
    }
}
