namespace ServiceControl.Federation.Configuration;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Settings for the federated API a primary instance hosts when federation is enabled. Each member is
/// one independent ServiceControl installation: a primary instance (with its own audit instances behind
/// it) and, optionally, a Monitoring instance.
/// </summary>
public class FederationSettings
{
    public const string DefaultPrimaryPathBase = "/federation/api";
    public const string DefaultMonitoringPathBase = "/federation/monitoring";

    public string DisplayName { get; set; } = "ServiceControl (federated)";

    /// <summary>
    /// The federated installations. The first member is the lead member: it answers instance-wide
    /// questions that cannot be aggregated, and its answer wins conflicts.
    /// </summary>
    public List<MemberSettings> Members { get; set; } = [];

    public TimeSpan MemberTimeout { get; set; } = TimeSpan.FromSeconds(4);

    /// <summary>
    /// Timeout for streamed transfers such as message bodies and usage report downloads.
    /// </summary>
    public TimeSpan TransferTimeout { get; set; } = TimeSpan.FromSeconds(60);

    public TimeSpan HealthProbeInterval { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>
    /// The deepest row a sorted page merge asks each member for. Pages beyond it fall back to the
    /// approximate per-member page union and say so in <c>X-Particular-Incomplete-Results</c>.
    /// </summary>
    public int DeepPagingMaxRows { get; set; } = 1000;

    public string PrimaryPathBase { get; set; } = DefaultPrimaryPathBase;

    public string MonitoringPathBase { get; set; } = DefaultMonitoringPathBase;

    public bool HasMonitoring => Members.Any(m => !string.IsNullOrWhiteSpace(m.MonitoringUrl));

    public void Validate()
    {
        if (Members.Count == 0)
        {
            throw new InvalidOperationException("Federation is enabled but no federated instances are configured. List them in the FederatedInstances setting.");
        }

        foreach (var member in Members)
        {
            if (string.IsNullOrWhiteSpace(member.Key) || !member.Key.All(c => char.IsAsciiLetterOrDigit(c) || (c is '-' or '_')))
            {
                throw new InvalidOperationException($"Federated instance key '{member.Key}' is invalid. Use letters, digits, '-' or '_'.");
            }

            if (!Uri.TryCreate(member.PrimaryUrl, UriKind.Absolute, out _))
            {
                throw new InvalidOperationException($"Federated instance '{member.Key}' has an invalid api_uri '{member.PrimaryUrl}'.");
            }

            if (!string.IsNullOrWhiteSpace(member.MonitoringUrl) && !Uri.TryCreate(member.MonitoringUrl, UriKind.Absolute, out _))
            {
                throw new InvalidOperationException($"Federated instance '{member.Key}' has an invalid monitoring_uri '{member.MonitoringUrl}'.");
            }
        }

        var duplicate = Members.GroupBy(m => m.Key, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
        if (duplicate != null)
        {
            throw new InvalidOperationException($"Federated instance key '{duplicate.Key}' is used more than once. Give the instances distinct 'key' values.");
        }

        if (DeepPagingMaxRows < 1)
        {
            throw new InvalidOperationException("DeepPagingMaxRows must be at least 1.");
        }
    }

    /// <summary>
    /// Parses the <c>FederatedInstances</c> setting, a JSON array in the style of <c>RemoteInstances</c>:
    /// <c>[{"api_uri": "http://host:33333/api/", "monitoring_uri": "http://host:33633/", "key": "eu", "name": "Europe"}]</c>.
    /// Only <c>api_uri</c> is required. A missing key is derived from the host and port of <c>api_uri</c>.
    /// </summary>
    public static MemberSettings[] ParseMembers(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        var members = JsonSerializer.Deserialize<MemberSettings[]>(json) ?? [];
        foreach (var member in members.Where(m => string.IsNullOrWhiteSpace(m.Key)))
        {
            member.Key = KeyFrom(member.PrimaryUrl);
        }

        return members;
    }

    /// <summary>
    /// A key that stays the same while the URL does, so ids that embed it remain routable.
    /// </summary>
    static string KeyFrom(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return "";
        }

        var key = new StringBuilder();
        foreach (var c in $"{uri.Host}-{uri.Port}".ToLowerInvariant())
        {
            key.Append((char.IsAsciiLetterOrDigit(c) || c == '-') ? c : '-');
        }

        return key.ToString();
    }
}

public class MemberSettings
{
    /// <summary>
    /// Short, stable identifier. It appears in synthesised ids such as <c>instance_id</c>, so changing it
    /// breaks links a client has cached.
    /// </summary>
    [JsonPropertyName("key")]
    public string Key { get; set; } = "";

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>
    /// The primary instance API root, for example <c>http://host:33333/api/</c>.
    /// </summary>
    [JsonPropertyName("api_uri")]
    public string PrimaryUrl { get; set; } = "";

    /// <summary>
    /// The Monitoring instance root, for example <c>http://host:33633/</c>. Optional.
    /// </summary>
    [JsonPropertyName("monitoring_uri")]
    public string? MonitoringUrl { get; set; }
}
