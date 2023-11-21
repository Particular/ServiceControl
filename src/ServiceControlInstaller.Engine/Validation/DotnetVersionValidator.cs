namespace ServiceControlInstaller.Engine.Validation
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Win32;

    public static class DotnetVersionValidator
    {
        public static bool FrameworkRequirementsAreMissing(out string message)
        {
            message = null;

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

            if (missing.Count > 0)
            {
                message = "ServiceControl cannot be installed because the system is missing the following prerequisites:"
                    + string.Join(string.Empty, missing.Select(msg => $"{Environment.NewLine}  - {msg}"));

                return true;
            }

            return false;
        }
    }
}
