#nullable enable
namespace ServiceControl.Infrastructure;

using System;
using ServiceControl.Configuration;

public static class MachineIdentity
{
    /// <summary>
    /// Containers report this instead of a hash: every container has its own hostname, so two
    /// containers on one host would never compare equal and the comparison would mislead.
    /// </summary>
    public const string NotApplicable = "NotApplicable";

    /// <summary>
    /// A stable hash of this machine's identity, <see cref="NotApplicable"/> in a container, or
    /// null when nothing stable was available.
    /// </summary>
    public static string? Hash => hash.Value;

    static readonly Lazy<string?> hash = new(Compute);

    static string? Compute()
    {
        if (AppEnvironment.RunningInContainer)
        {
            return NotApplicable;
        }

        var machineName = Environment.MachineName;

        return string.IsNullOrWhiteSpace(machineName) ? null : IdentityHash.Compute("machine-name:" + machineName);
    }
}
