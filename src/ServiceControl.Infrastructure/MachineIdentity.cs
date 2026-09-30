#nullable enable
namespace ServiceControl.Infrastructure;

using System;
using System.IO;
using ServiceControl.Configuration;

public static class MachineIdentity
{
    /// <summary>
    /// Containers report this instead of a hash: every container has its own machine id, so two
    /// containers on one host would never compare equal and the comparison would mislead.
    /// </summary>
    public const string NotApplicable = "NotApplicable";

    /// <summary>
    /// A stable hash of this machine's identity, <see cref="NotApplicable"/> in a container, or
    /// null when nothing stable was available. The source is prefixed into the hashed value so that
    /// identities from different sources can never compare equal by accident.
    /// </summary>
    public static string? Hash => hash.Value;

    static readonly Lazy<string?> hash = new(Compute);

    static string? Compute()
    {
        if (AppEnvironment.RunningInContainer)
        {
            return NotApplicable;
        }

        if (OperatingSystem.IsLinux())
        {
            foreach (var path in MachineIdPaths)
            {
                try
                {
                    var machineId = File.ReadAllText(path).Trim();

                    if (machineId.Length > 0)
                    {
                        return IdentityHash.Compute("machine-id:" + machineId);
                    }
                }
                catch (Exception)
                {
                }
            }
        }

        // The machine name rather than the Windows MachineGuid, which only the registry serves and
        // would cost this cross-platform assembly a Windows-only package dependency.
        var machineName = Environment.MachineName;

        return string.IsNullOrWhiteSpace(machineName) ? null : IdentityHash.Compute("machine-name:" + machineName);
    }

    static readonly string[] MachineIdPaths = ["/etc/machine-id", "/var/lib/dbus/machine-id"];
}
