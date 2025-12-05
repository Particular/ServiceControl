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

        if (!Enabled)
        {
            return;
        }

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
        if (!Enabled)
        {
            logger.LogInformation("Forwarded headers processing is disabled");
            return;
        }

        logger.LogInformation("Forwarded headers configuration:");
        logger.LogInformation("  Enabled: {Enabled}", Enabled);
        logger.LogInformation("  TrustAllProxies: {TrustAllProxies}", TrustAllProxies);

        if (KnownProxiesRaw.Count > 0)
        {
            logger.LogInformation("  KnownProxies: {KnownProxies}", string.Join(", ", KnownProxiesRaw));
        }

        if (KnownNetworks.Count > 0)
        {
            logger.LogInformation("  KnownNetworks: {KnownNetworks}", string.Join(", ", KnownNetworks));
        }

        if (TrustAllProxies)
        {
            logger.LogWarning("ForwardedHeaders.TrustAllProxies is true. Any client can spoof X-Forwarded-* headers. Consider configuring KnownProxies or KnownNetworks for production environments.");
        }
    }
}
