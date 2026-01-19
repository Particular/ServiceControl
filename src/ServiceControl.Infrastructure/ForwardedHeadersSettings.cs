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

    public bool Enabled { get; }

    public bool TrustAllProxies { get; private set; }

    // Store as strings for serialization compatibility, parse to IPAddress when needed
    public List<string> KnownProxiesRaw { get; } = [];

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

        if (!Enabled)
        {
            if (hasProxyConfig || TrustAllProxies)
            {
                logger.LogWarning("Forwarded headers processing is disabled. Proxy configuration settings will be ignored: {@Settings}",
                    new { Enabled, TrustAllProxies, KnownProxies = KnownProxiesRaw, KnownNetworks });
            }
            else
            {
                logger.LogInformation("Forwarded headers processing is disabled");
            }
            return;
        }

        if (TrustAllProxies)
        {
            if (hasProxyConfig)
            {
                // This shouldn't happen due to constructor logic, but log if it does
                logger.LogWarning("Forwarded headers is configured to trust all proxies. KnownProxies and KnownNetworks settings will be ignored: {@Settings}",
                    new { Enabled, TrustAllProxies, KnownProxies = KnownProxiesRaw, KnownNetworks });
            }
            else
            {
                logger.LogWarning("Forwarded headers is configured to trust all proxies. Any client can spoof X-Forwarded-* headers. Consider configuring KnownProxies or KnownNetworks for production environments: {@Settings}",
                    new { Enabled, TrustAllProxies });
            }
        }
        else if (hasProxyConfig)
        {
            logger.LogInformation("Forwarded headers is configured with specific trusted proxies: {@Settings}",
                new { Enabled, TrustAllProxies, KnownProxies = KnownProxiesRaw, KnownNetworks });
        }
        else
        {
            logger.LogWarning("Forwarded headers is enabled but no trusted proxies are configured. X-Forwarded-* headers will not be processed: {@Settings}",
                new { Enabled, TrustAllProxies, KnownProxies = KnownProxiesRaw, KnownNetworks });
        }
    }
}
