namespace ServiceControlInstaller.Engine
{
    using System;
    using System.Linq;
    using System.ServiceProcess;
    using System.Threading.Tasks;
    using ServiceControl.LicenseManagement;
    using ServiceControlInstaller.Engine.Configuration.ServiceControl;
    using ServiceControlInstaller.Engine.Instances;
    using ServiceControlInstaller.Engine.Validation;

    public abstract class AbstractCommandChecks
    {
        protected const string UpgradeGuide4to5Url = "https://docs.particular.net/servicecontrol/upgrades/4to5/";

        protected abstract Task<bool> PromptForRabbitMqCheck(bool isUpgrade);
        protected abstract Task<bool> PromptToStopRunningInstance(BaseService instance);
        protected abstract Task NotifyForDeprecatedMessageTransport(TransportInfo transport);
        protected abstract Task NotifyForMissingSystemPrerequisites(string missingPrereqsMessage);
        protected abstract Task NotifyForIncompatibleStorageEngine(IServiceControlBaseInstance baseInstance);
        protected abstract Task NotifyForIncompatibleUpgradeVersion(UpgradeInfo upgradeInfo);
        protected abstract Task NotifyForLicenseIssue(string licenseMessage);

        public async Task<bool> CanAddInstance(bool needsRavenDB)
        {
            // Check for license
            if (!await IsLicenseOk().ConfigureAwait(false))
            {
                return false;
            }

            if (await FrameworkRequirementsAreMissing(needsRavenDB).ConfigureAwait(false))
            {
                return false;
            }

            return true;
        }

        public async Task<bool> ValidateNewInstance(params IServiceInstance[] instances)
        {
            var transport = instances
                .OfType<ITransportConfig>()
                .Where(i => i is not null)
                .Select(i => i.TransportPackage)
                .First(t => t is not null);

            var continueInstall = await RabbitMqCheckIsOK(transport, false).ConfigureAwait(false);

            return continueInstall;
        }

        async Task<bool> RabbitMqCheckIsOK(TransportInfo transport, bool isUpgrade)
        {
            if (transport is null)
            {
                throw new ArgumentNullException(nameof(transport));
            }

            if (transport.ZipName != "RabbitMQ")
            {
                // not Rabbit, don't care
                return true;
            }

            // Only way we DON'T need to warn is if we're updating an instance that's already on a "new" (AvailableInSCMU) Rabbit transport
            var needToWarn = !(isUpgrade && transport.AvailableInSCMU);
            if (!needToWarn)
            {
                return true;
            }

            return await PromptForRabbitMqCheck(isUpgrade).ConfigureAwait(false);
        }

        public async Task<bool> CanUpgradeInstance(BaseService instance, bool forceUpgradeDb = false)
        {
            // Check for license
            if (!await IsLicenseOk().ConfigureAwait(false))
            {
                return false;
            }

            // Check for transports that can't be upgraded
            var cantUpdateTransport = instance.TransportPackage.Removed && instance.TransportPackage.AutoMigrateTo is null;
            if (cantUpdateTransport)
            {
                await NotifyForDeprecatedMessageTransport(instance.TransportPackage).ConfigureAwait(false);
                return false;
            }

            // Validate .NET Framework requirements
            bool needsRavenDB = instance is IServiceControlBaseInstance;
            if (await FrameworkRequirementsAreMissing(needsRavenDB).ConfigureAwait(false))
            {
                return false;
            }

            // RavenDB 5+ check
            if (instance is IServiceControlBaseInstance baseInstance)
            {
                if (!forceUpgradeDb)
                {
                    var compatibleStorageEngine = baseInstance.PersistenceManifest.Name == StorageEngineNames.RavenDB;

                    if (!compatibleStorageEngine)
                    {
                        await NotifyForIncompatibleStorageEngine(baseInstance).ConfigureAwait(false);
                        return false;
                    }
                }

                // To be clear, Monitoring doesn't bother with this check because it's all in-memory storage
                // so you could hypothetically change to any version at any time
                var upgradeInfo = UpgradeInfo.GetUpgradePathFor(instance.Version);
                if (upgradeInfo.HasIncompatibleVersion)
                {
                    await NotifyForIncompatibleUpgradeVersion(upgradeInfo).ConfigureAwait(false);
                    return false;
                }
            }

            if (!await RabbitMqCheckIsOK(instance.TransportPackage, isUpgrade: true).ConfigureAwait(false))
            {
                return false;
            }

            return true;
        }

        async Task<bool> FrameworkRequirementsAreMissing(bool needsRavenDB)
        {
            if (DotnetVersionValidator.FrameworkRequirementsAreMissing(needsRavenDB, out var missingMessage))
            {
                await NotifyForMissingSystemPrerequisites(missingMessage).ConfigureAwait(false);
                return true;
            }

            return false;
        }

        async Task<bool> IsLicenseOk()
        {
            var licenseCheckResult = CheckLicenseIsValid();
            if (!licenseCheckResult.Valid)
            {
                await NotifyForLicenseIssue($"Upgrade could not continue due to an issue with the current license. {licenseCheckResult.Message}.  Contact contact@particular.net").ConfigureAwait(false);
                return false;
            }

            return true;
        }

        public async Task<bool> StopBecauseInstanceIsRunning(BaseService instance)
        {
            if (instance.Service.Status == ServiceControllerStatus.Stopped)
            {
                return false;
            }

            var proceed = await PromptToStopRunningInstance(instance).ConfigureAwait(false);

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

            var releaseDate = LicenseManager.GetReleaseDate();

            if (license.Details.ReleaseNotCoveredByMaintenance(releaseDate))
            {
                return new CheckLicenseResult(false, "License does not cover this release of ServiceControl Monitoring. Upgrade protection expired.");
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
