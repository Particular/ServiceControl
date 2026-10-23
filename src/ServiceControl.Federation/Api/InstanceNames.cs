namespace ServiceControl.Federation.Api;

using System;
using System.Text.Json.Nodes;
using Aggregation;
using Members;

/// <summary>
/// Qualifies ServiceControl instance names with the member they belong to. Installations usually keep
/// the default instance names, and ServicePulse attributes internal custom checks to platform health
/// rows by name alone, so unqualified names would attach one installation's checks to every
/// installation's rows.
/// </summary>
static class InstanceNames
{
    public static string Qualify(string name, Member member)
    {
        var suffix = $" ({member.Name})";
        return name.EndsWith(suffix, StringComparison.Ordinal) ? name : name + suffix;
    }

    /// <summary>
    /// Qualifies <c>host.instance_name</c> in a configuration document.
    /// </summary>
    public static void QualifyConfiguration(JsonNode? configuration, Member member)
    {
        if (JsonValues.At(configuration, "host") is JsonObject host && JsonValues.Text(host, "instance_name") is { } name)
        {
            host["instance_name"] = Qualify(name, member);
        }
    }

    /// <summary>
    /// Qualifies the originating endpoint of a check the member's own instances report, so it keeps
    /// matching the qualified instance name.
    /// </summary>
    public static void QualifyInternalCheck(JsonNode row, Member member)
    {
        if (JsonValues.Bool(row, "internal") == true
            && JsonValues.At(row, "originating_endpoint") is JsonObject endpoint
            && JsonValues.Text(endpoint, "name") is { } name)
        {
            endpoint["name"] = Qualify(name, member);
        }
    }
}
