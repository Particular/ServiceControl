namespace ServiceControlInstaller.Engine
{
    using System;
    using System.Linq;
    using System.ServiceProcess;
    using System.Threading;
    using System.Threading.Tasks;
    using NuGet.Versioning;
    using ServiceControl.LicenseManagement;
    using ServiceControlInstaller.Engine.Configuration.ServiceControl;
    using ServiceControlInstaller.Engine.Instances;
    using ServiceControlInstaller.Engine.Validation;

    public abstract class AbstractCommandChecks
    {
        protected const string UpgradeGuide4to5Url = "https://docs.particular.net/servicecontrol/upgrades/4to5/";

        protected abstract Task<bool> PromptForRabbitMqCheck(bool isUpgrade, CancellationToken cancellationToken = default);
        protected abstract Task<bool> PromptToStopRunningInstance(BaseService instance, CancellationToken cancellationToken = default);
        protected abstract Task<bool> PromptToContinueWithForcedUpgrade(CancellationToken cancellationToken = default);
        protected abstract Task NotifyForDeprecatedMessageTransport(TransportInfo transport, CancellationToken cancellationToken = default);
        protected abstract Task NotifyForMissingSystemPrerequisites(string missingPrereqsMessage, CancellationToken cancellationToken = default);
        protected abstract Task NotifyForIncompatibleStorageEngine(IServiceControlBaseInstance baseInstance, CancellationToken cancellationToken = default);
        protected abstract Task NotifyForIncompatibleUpgradeVersion(UpgradeInfo upgradeInfo, CancellationToken cancellationToken = default);
        protected abstract Task NotifyError(string title, string message, CancellationToken cancellationToken = default);

        public async Task<bool> CanAddInstance(bool allowExpiredLicense = false, CancellationToken cancellationToken = default)
        {
            // Check for license
            if (!allowExpiredLicense && !await IsLicenseOk(cancellationToken).ConfigureAwait(false))
            {
                return false;
            }

            if (await OldVersionOfServiceControlInstalled(cancellationToken).ConfigureAwait(false))
            {
                return false;
            }

            return true;
        }

        public async Task<bool> ValidateNewInstance(IServiceInstance[] instances, CancellationToken cancellationToken = default)
        {
            var transport = instances
                .OfType<ITransportConfig>()
                .Where(i => i is not null)
                .Select(i => i.TransportPackage)
                .First(t => t is not null);

            var continueInstall = await RabbitMqCheckIsOK(transport, Constants.CurrentVersion, false, cancellationToken).ConfigureAwait(false);

            return continueInstall;
        }

        public Task<bool> CanEditInstance(BaseService instance, CancellationToken cancellationToken = default) => CanEditOrDelete(instance, isDelete: false, cancellationToken);

        public Task<bool> CanDeleteInstance(BaseService instance, CancellationToken cancellationToken = default) => CanEditOrDelete(instance, isDelete: true, cancellationToken);

        async Task<bool> CanEditOrDelete(BaseService instance, bool isDelete, CancellationToken cancellationToken)
        {
            var instanceVersion = instance.Version;
            var instanceIsNewer = instanceVersion > Constants.CurrentVersion;
            var installerOfDifferentMajor = instanceVersion.Major != Constants.CurrentVersion.Major;

            const string title = "Incompatible installer version";

            if (instanceIsNewer)
            {
                var verb = isDelete ? "remove" : "edit";
                var message = $"This instance version {instanceVersion} is newer than the installer version {Constants.CurrentVersion}. This installer can only {verb} instances with versions between {Constants.CurrentVersion.Major}.0.0 and {Constants.CurrentVersion}.";
                await NotifyError(title, message, cancellationToken).ConfigureAwait(false);
                return false;
            }

            if (installerOfDifferentMajor && !isDelete)
            {
                var message = $"This installer cannot edit instances created by a different major version. A {instanceVersion.Major}.* installer version greater or equal to {instanceVersion.Major}.{instanceVersion.Minor}.{instanceVersion.Patch} must be used instead.";
                await NotifyError(title, message, cancellationToken).ConfigureAwait(false);
                return false;
            }

            return true;
        }

        async Task<bool> RabbitMqCheckIsOK(TransportInfo transport, SemanticVersion instanceVersion, bool isUpgrade, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(transport);

            if (transport.ZipName != "RabbitMQ")
            {
                // not Rabbit, don't care
                return true;
            }

            var newerThan650 = VersionComparer.Version.Compare(instanceVersion, new SemanticVersion(6, 5, 0)) > 0;

            if (isUpgrade && newerThan650)
            {
                return true;
            }

            return await PromptForRabbitMqCheck(isUpgrade, cancellationToken).ConfigureAwait(false);
        }

        public async Task<bool> CanUpgradeInstance(BaseService instance, bool forceUpgradeDb = false, CancellationToken cancellationToken = default)
        {
            // Check for license
            if (!await IsLicenseOk(cancellationToken).ConfigureAwait(false))
            {
                return false;
            }

            if (await OldVersionOfServiceControlInstalled(cancellationToken).ConfigureAwait(false))
            {
                return false;
            }

            // Check for transports that can't be upgraded
            var cantUpdateTransport = instance.TransportPackage.Removed && instance.TransportPackage.AutoMigrateTo is null;
            if (cantUpdateTransport)
            {
                await NotifyForDeprecatedMessageTransport(instance.TransportPackage, cancellationToken).ConfigureAwait(false);
                return false;
            }

            // RavenDB 5+ check
            if (instance is IServiceControlBaseInstance baseInstance)
            {
                if (forceUpgradeDb)
                {
                    var forceUpgradeAllowed = instance is IPersistenceConfig persistenceCfg
                        && instance.Version.Major == 4
                        && persistenceCfg.PersistenceManifest.Name != StorageEngineNames.RavenDB;

                    if (!forceUpgradeAllowed)
                    {
                        await NotifyError("Cannot run the command", "Only ServiceControl 4.x primary instances that use RavenDB 3.5 persistence can be force-upgraded.", cancellationToken).ConfigureAwait(false);
                        return false;
                    }

                    if (!await PromptToContinueWithForcedUpgrade(cancellationToken).ConfigureAwait(false))
                    {
                        return false;
                    }
                }
                else
                {
                    var compatibleStorageEngine = baseInstance.PersistenceManifest.Name == StorageEngineNames.RavenDB;

                    if (!compatibleStorageEngine)
                    {
                        await NotifyForIncompatibleStorageEngine(baseInstance, cancellationToken).ConfigureAwait(false);
                        return false;
                    }
                }

                // To be clear, Monitoring doesn't bother with this check because it's all in-memory storage
                // so you could hypothetically change to any version at any time
                var upgradeInfo = UpgradeInfo.GetUpgradePathFor(instance.Version);
                if (upgradeInfo.HasIncompatibleVersion)
                {
                    await NotifyForIncompatibleUpgradeVersion(upgradeInfo, cancellationToken).ConfigureAwait(false);
                    return false;
                }
            }

            if (!await RabbitMqCheckIsOK(instance.TransportPackage, instance.Version, isUpgrade: true, cancellationToken).ConfigureAwait(false))
            {
                return false;
            }

            return true;
        }

        async Task<bool> OldVersionOfServiceControlInstalled(CancellationToken cancellationToken)
        {
            if (OldScmuCheck.OldVersionOfServiceControlInstalled(out var installedVersion))
            {
                var message = $"An old version {installedVersion} of ServiceControl Management is installed, which will not work after installing new instances. Before installing ServiceControl 5 instances, you must either uninstall the {installedVersion} instance or update it to a 4.x version at least {OldScmuCheck.MinimumScmuVersion}.";
                await NotifyError("Outdated Version Installed", message, cancellationToken).ConfigureAwait(false);
                return true;
            }

            return false;
        }

        async Task<bool> IsLicenseOk(CancellationToken cancellationToken)
        {
            var licenseCheckResult = CheckLicenseIsValid();
            if (!licenseCheckResult.Valid)
            {
                await NotifyError("License Error", $"Upgrade could not continue due to an issue with the current license. {licenseCheckResult.Message}.  Contact contact@particular.net", cancellationToken).ConfigureAwait(false);
                return false;
            }

            return true;
        }

        public async Task<bool> StopBecauseInstanceIsRunning(BaseService instance, CancellationToken cancellationToken = default)
        {
            if (instance.Service.Status == ServiceControllerStatus.Stopped)
            {
                return false;
            }

            var proceed = await PromptToStopRunningInstance(instance, cancellationToken).ConfigureAwait(false);

            return !proceed;
        }

        CheckLicenseResult CheckLicenseIsValid()
        {
            var license = LicenseManager.FindLicense();

            if (license.Details.HasLicenseExpired())
            {
                return new CheckLicenseResult(false, "License has expired");
            }

            if (!license.Details.ValidForServiceControl)
            {
                return new CheckLicenseResult(false, "This license edition does not include ServiceControl");
            }

            if (license.Details.Status.Contains("InvalidDueToUpgradeProtectionNoLongerBeingSupported", StringComparison.OrdinalIgnoreCase))
            {
                return new CheckLicenseResult(false, "Licenses with upgrade protection are no longer supported.");
            }

            return new CheckLicenseResult(true);
        }

        class CheckLicenseResult
        {
            public CheckLicenseResult(bool valid, string message = null)
            {
                Valid = valid;
                Message = message;
            }

            public bool Valid { get; private set; }

            public string Message { get; private set; }
        }
    }
}
