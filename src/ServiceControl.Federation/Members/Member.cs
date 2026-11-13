namespace ServiceControl.Federation.Members;

using System;
using Configuration;

/// <summary>
/// Which of a member's two HTTP surfaces a request targets.
/// </summary>
public enum Surface
{
    Primary,
    Monitoring
}

/// <summary>
/// Runtime view of one configured installation, including its last observed health and version.
/// </summary>
public sealed class Member : IEquatable<Member>
{
    public Member(MemberSettings settings, bool isLead)
    {
        Key = settings.Key;
        Name = string.IsNullOrWhiteSpace(settings.Name) ? settings.Key : settings.Name;
        PrimaryUri = WithTrailingSlash(settings.PrimaryUrl);
        MonitoringUri = string.IsNullOrWhiteSpace(settings.MonitoringUrl) ? null : WithTrailingSlash(settings.MonitoringUrl);
        IsLead = isLead;
    }

    public string Key { get; }

    public string Name { get; }

    public Uri PrimaryUri { get; }

    public Uri? MonitoringUri { get; }

    public bool IsLead { get; }

    public Uri? BaseUri(Surface surface) => surface == Surface.Primary ? PrimaryUri : MonitoringUri;

    public bool Serves(Surface surface) => BaseUri(surface) != null;

    /// <summary>
    /// Set when a connection attempt failed. Requests skip the member until the health probe succeeds,
    /// so a dead member does not cost a timeout on every poll.
    /// </summary>
    public bool IsUnavailable(Surface surface) => surface == Surface.Primary ? primaryUnavailable : monitoringUnavailable;

    public void MarkUnavailable(Surface surface, bool unavailable)
    {
        if (surface == Surface.Primary)
        {
            primaryUnavailable = unavailable;
        }
        else
        {
            monitoringUnavailable = unavailable;
        }
    }

    public string? Version(Surface surface) => surface == Surface.Primary ? primaryVersion : monitoringVersion;

    public void RecordVersion(Surface surface, string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return;
        }

        if (surface == Surface.Primary)
        {
            primaryVersion = version;
        }
        else
        {
            monitoringVersion = version;
        }
    }

    public override string ToString() => Key;

    public bool Equals(Member? other) => other != null && string.Equals(Key, other.Key, StringComparison.OrdinalIgnoreCase);

    public override bool Equals(object? obj) => Equals(obj as Member);

    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Key);

    static Uri WithTrailingSlash(string url) => new(url.EndsWith('/') ? url : url + "/");

    volatile bool primaryUnavailable;
    volatile bool monitoringUnavailable;
    volatile string? primaryVersion;
    volatile string? monitoringVersion;
}
