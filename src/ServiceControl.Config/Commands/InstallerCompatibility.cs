namespace ServiceControl.Config.Commands
{
    using System;
    using System.Threading.Tasks;
    using Framework;

    static class InstallerVersionCompatibilityDialog
    {
        public static async Task<bool> ShowValidation(Version instanceVersion, Version installerVersion, IServiceControlWindowManager windowManager)
        {
            var instanceIsNewer = instanceVersion > installerVersion;
            var installerOfDifferentMajor = instanceVersion.Major != installerVersion.Major;

            if (instanceIsNewer)
            {
                await windowManager.ShowMessage(
                    "Incompatible installer version",
                    $"This instance version {instanceVersion} is newer than the installer version {installerVersion}. This installer can only edit instances with versions between {installerVersion.Major}.0.0 and {installerVersion}."
                    );
                return true;
            }

            if (installerOfDifferentMajor)
            {
                await windowManager.ShowMessage(
                    "Incompatible installer version",
                    $"This installer cannot edit instances created by a different major version. This instance can be edited by a {instanceVersion.Major}.* installer version greater or equal to {instanceVersion.Major}.{instanceVersion.Minor}.{instanceVersion.Build}."
                    );
                return true;
            }

            return false;
        }
    }
}