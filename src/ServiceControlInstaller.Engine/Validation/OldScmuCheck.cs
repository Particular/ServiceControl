namespace ServiceControlInstaller.Engine.Validation
{
    using System;
    using System.Linq;
    using Microsoft.Win32;

    /// <summary>
    /// Even though <see cref="ServiceControlInstaller.Engine.Configuration.ServiceControl.UpgradeInfo"/> contains
    /// the upgrade path, and that path contains 4.33.0 as a step, this check still needs to exist to validate that
    /// either 1) An AdvancedInstaller-based-SCMU is not installed on the system, or 2) The AdvancedInstaller-SCMU
    /// is version 4.33.x and can understand ServiceControl 5+ transport/persistence type names.
    /// </summary>
    public static class OldScmuCheck
    {
        public static readonly Version MinimumScmuVersion = new Version(4, 33, 0);

        public static bool OldVersionOfServiceControlInstalled(out string installedVersion)
        {
            installedVersion = null;

            // Example Registry key: 'HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\ServiceControl 4.33.0'
            using var uninstallKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");

            var serviceControlVersion = uninstallKey.GetSubKeyNames()
                .Where(subKeyName => subKeyName.StartsWith("ServiceControl "))
                .Select(subKeyName =>
                {
                    using var appKey = uninstallKey.OpenSubKey(subKeyName);

                    var versionText = appKey.GetValue("DisplayVersion") as string;

                    return Version.TryParse(versionText, out var version) ? version : null;
                })
                .Where(v => v is not null)
                .OrderByDescending(v => v)
                .FirstOrDefault();

            if (serviceControlVersion is null)
            {
                return false;
            }

            installedVersion = serviceControlVersion.ToString();

            if (serviceControlVersion >= MinimumScmuVersion)
            {
                return false;
            }

            return true;
        }
    }
}
