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
                    "Incompatible installer to edit configuration for newer minor/patch release",
                    $"Instance version {instanceVersion} is newer than installer version {installerVersion}. Installer can only edit instances for the same version or older versions (between {installerVersion.Major}.0.0 - and {installerVersion} for this major."
                    );
                return true;
            }

            if (installerOfDifferentMajor)
            {
                await windowManager.ShowMessage(
                    "Incompatible installer to edit configuration of different major release",
                    $"Installer cannot edit configurations for installers of a different major version. Use installer version {instanceVersion} or a newer {instanceVersion.Major}.minor.patch to edit the configuration of this instance."
                    );
                return true;
            }

            return false;
        }
    }
}