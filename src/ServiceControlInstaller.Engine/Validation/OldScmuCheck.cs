namespace ServiceControlInstaller.Engine.Validation
{
    using System;
    using Microsoft.Win32;

    public static class OldScmuCheck
    {
        public static bool OldVersionOfServiceControlInstalled(out string installedVersion)
        {
            installedVersion = null;

            using var regKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\ParticularSoftware\ServiceControl");
            if (regKey == null)
            {
                return false;
            }

            installedVersion = regKey.GetValue("Version") as string;
            if (!Version.TryParse(installedVersion, out var version))
            {
                return false;
            }

            if (version >= new Version(4, 33, 0))
            {
                return false;
            }

            return true;
        }
    }
}
