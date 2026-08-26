namespace Particular.ServiceControl;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting.WindowsServices;
using Particular.LicensingComponent.Contracts;
using global::ServiceControl.Configuration;

class HostEnvironmentDataProvider : IEnvironmentDataProvider
{
    public Task<IEnumerable<(string key, string value)>> GetData(CancellationToken cancellationToken = default) =>
        Task.FromResult<IEnumerable<(string, string)>>(
        [
            ("Host.Model", HostModel()),
            ("Host.Orchestrator", Environment.GetEnvironmentVariable(KubernetesServiceHostVariable) is not null ? "Kubernetes" : "None"),
            ("Host.OSPlatform", OSPlatformName()),
            ("Host.OSVersion", $"{Environment.OSVersion.Version.Major}.{Environment.OSVersion.Version.Minor}"),
            ("Host.Architecture", RuntimeInformation.ProcessArchitecture.ToString()),
            ("Host.RuntimeVersion", Environment.Version.ToString(3)),
            ("Host.ProcessorCount", Environment.ProcessorCount.ToString(CultureInfo.InvariantCulture)),
            ("Host.AvailableMemoryGB", AvailableMemoryGB())
        ]);

    static string HostModel()
    {
        if (AppEnvironment.RunningInContainer)
        {
            return "Container";
        }

        return WindowsServiceHelpers.IsWindowsService() ? "WindowsService" : "Console";
    }

    static string OSPlatformName()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return "Windows";
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return "Linux";
        }

        return RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "macOS" : "Unknown";
    }

    // TotalAvailableMemoryBytes honours the cgroup limit, so a container reports the memory it is
    // limited to rather than the physical memory of the machine hosting it.
    static string AvailableMemoryGB()
    {
        var totalAvailableMemoryBytes = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;

        return totalAvailableMemoryBytes <= 0
            ? "Unknown"
            : Math.Round(totalAvailableMemoryBytes / (double)BytesPerGigabyte, MidpointRounding.AwayFromZero).ToString("F0", CultureInfo.InvariantCulture);
    }

    const string KubernetesServiceHostVariable = "KUBERNETES_SERVICE_HOST";
    const long BytesPerGigabyte = 1024L * 1024 * 1024;
}
