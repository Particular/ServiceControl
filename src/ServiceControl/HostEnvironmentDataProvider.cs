namespace Particular.ServiceControl;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Hosting.WindowsServices;
using Particular.LicensingComponent.Contracts;
using global::ServiceControl.Configuration;
using static Particular.LicensingComponent.Contracts.EnvironmentDatum;

class HostEnvironmentDataProvider : IEnvironmentDataProvider
{
    public IEnumerable<EnvironmentDatum> GetData() =>
    [
        Value("Host.Model", HostModel),
        Value("Host.Orchestrator", Orchestrator),
        Value("Host.OSPlatform", OSPlatformName),
        Value("Host.OSVersion", () => $"{Environment.OSVersion.Version.Major}.{Environment.OSVersion.Version.Minor}"),
        Value("Host.Architecture", () => RuntimeInformation.ProcessArchitecture.ToString()),
        Value("Host.RuntimeVersion", () => Environment.Version.ToString(3)),
        Value("Host.ProcessorCount", () => Environment.ProcessorCount.ToString(CultureInfo.InvariantCulture)),
        Value("Host.AvailableMemoryGB", AvailableMemoryGB)
    ];

    static string Orchestrator() => Environment.GetEnvironmentVariable(KubernetesServiceHostVariable) is not null ? "Kubernetes" : "None";

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
