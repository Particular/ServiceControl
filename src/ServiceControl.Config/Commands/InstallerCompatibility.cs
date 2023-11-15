namespace ServiceControl.Config.Commands
{
    using System.Threading.Tasks;
    using Framework;
    using NuGet.Versioning;
    using ServiceControlInstaller.Engine.Instances;

    static class InstallerVersionCompatibilityDialog
    {
        public static async Task<bool> ShowValidation(SemanticVersion instanceVersion, IServiceControlWindowManager windowManager)
        {
            var instanceIsNewer = instanceVersion > Constants.CurrentVersion;
            var installerOfDifferentMajor = instanceVersion.Major != Constants.CurrentVersion.Major;

            if (instanceIsNewer)
            {
                await windowManager.ShowMessage(
                    "Incompatible installer version",
                    $"This instance version {instanceVersion} is newer than the installer version {Constants.CurrentVersion}. This installer can only edit or remove instances with versions between {Constants.CurrentVersion.Major}.0.0 and {Constants.CurrentVersion}.",
                    hideCancel: true
                    );
                return true;
            }

            if (installerOfDifferentMajor)
            {
                await windowManager.ShowMessage(
                    "Incompatible installer version",
                    $"This installer cannot edit or remove instances created by a different major version. This instance can be edited by a {instanceVersion.Major}.* installer version greater or equal to {instanceVersion.ToNormalizedString()}.",
                    hideCancel: true
                    );
                return true;
            }

            return false;
        }
    }
}