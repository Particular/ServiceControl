namespace ServiceControlInstaller.Engine.Validation
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Win32;

    public static class DotnetVersionValidator
    {
        public const string MinimumVersionString = "7.0.13";

        public static bool FrameworkRequirementsAreMissing(bool needsRavenDB, out string message)
        {
            message = null;

            var dotnetMinVersion = Version.Parse(MinimumVersionString);
            var majorMinor = $"{dotnetMinVersion.Major}.{dotnetMinVersion.Minor}";

            if (!Environment.Is64BitProcess)
            {
                message = "ServiceControl can only be installed on a 64-bit OS.";
                return true;
            }

            var missing = new List<string>();
            using var registry = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);

            // Check for .NET Framework 4.7.2 - will go away in upcoming .NET version
            using var netFxKey = registry.OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full");
            var netFxValue = netFxKey?.GetValue("Release") as int?;
            if (netFxValue is null or < 461813)
            {
                missing.Add(".NET Framework 4.7.2 or later: Download from https://dotnet.microsoft.com/download/dotnet-framework");
            }

            if (needsRavenDB) // Monitoring instances don't need RavenDB and (for now) only need .NET Framework
            {
                // .NET itself requires Visual C++ Redistributable on Windows 2012 or earlier: https://learn.microsoft.com/en-us/dotnet/core/install/windows?tabs=net70#additional-deps
                // Server 2016 reports as 10.* while Server 2012 reports as 6.x
                if (Environment.OSVersion.Platform == PlatformID.Win32NT && Environment.OSVersion.Version.Major < 10)
                {
                    // Key to use from https://learn.microsoft.com/en-us/cpp/windows/redistributing-visual-cpp-files?view=msvc-170#install-the-redistributable-packages
                    // Exact version isn't even that important so we don't check it
                    using var visualCppRedistributableKey = registry.OpenSubKey(@"SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\X64");
                    var installedValue = (int?)visualCppRedistributableKey?.GetValue("Installed");
                    if (installedValue != 1)
                    {
                        missing.Add("Microsoft Visual C++ 2015-2022 Redistributable (x64): Download from https://aka.ms/vs/17/release/vc_redist.x64.exe");
                    }
                }

                // Check for .NET Version
                using var dotnetKey = registry.OpenSubKey(@"SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.NETCore.App")
                    ?? registry.OpenSubKey(@"SOFTWARE\WOW6432NODE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.NETCore.App");

                var dotnetNames = dotnetKey?.GetValueNames() ?? Array.Empty<string>();
                var dotnetClosest = HighestMatchingMajorMinor(dotnetMinVersion, dotnetNames);
                var dotnetOk = DotnetVersionOk(dotnetMinVersion, dotnetClosest);
                if (!dotnetOk)
                {
                    var foundString = dotnetClosest != null ? $", found {dotnetClosest}" : string.Empty;
                    missing.Add($".NET {dotnetMinVersion.Major} Runtime (x64), requires {dotnetMinVersion} or greater, found {dotnetClosest}: Download from https://dotnet.microsoft.com/download/dotnet/{majorMinor}");
                }

                // Check for ASP.NET Core
                var aspOk = false;
                string aspClosest = null;
                using var aspNetKey = registry.OpenSubKey($@"SOFTWARE\Microsoft\ASP.NET Core\Shared Framework");
                if (aspNetKey != null)
                {
                    var aspNetVersions = aspNetKey.GetSubKeyNames()
                        .Where(key => key.StartsWith("v"))
                        .SelectMany(key => aspNetKey.OpenSubKey(key).GetSubKeyNames())
                        .ToArray();

                    aspClosest = HighestMatchingMajorMinor(dotnetMinVersion, aspNetVersions);
                    aspOk = DotnetVersionOk(dotnetMinVersion, aspClosest);
                }
                if (!aspOk)
                {
                    var foundString = aspClosest != null ? $", found {aspClosest}" : string.Empty;
                    missing.Add($"ASP.NET Core {dotnetMinVersion.Major} Runtime (x64), requires {dotnetMinVersion} or greater{foundString}: Download from https://dotnet.microsoft.com/download/dotnet/{majorMinor}");
                }
            }

            if (missing.Count > 0)
            {
                message = "ServiceControl cannot be installed because the system is missing the following prerequisites:"
                    + string.Join(string.Empty, missing.Select(msg => $"{Environment.NewLine}  - {msg}"));

                return true;
            }

            return false;
        }

        static string HighestMatchingMajorMinor(Version requestedVersion, string[] versionChoices)
        {
            return versionChoices.Select(v => Version.TryParse(v, out var version) ? version : null)
                .Where(v => v != null && v.Major <= requestedVersion.Major)
                .OrderByDescending(v => v)
                .FirstOrDefault()
                ?.ToString();
        }

        internal static bool DotnetVersionOk(Version requestedVersion, string actualVersionString)
        {
            return Version.TryParse(actualVersionString, out var actualVersion)
                && actualVersion.Major == requestedVersion.Major
                && actualVersion.Minor == requestedVersion.Minor
                && actualVersion >= requestedVersion;
        }
    }
}
