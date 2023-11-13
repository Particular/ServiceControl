namespace ServiceControlInstaller.Engine.Unattended
{
    using System;
    using System.ServiceProcess;
    using System.Threading.Tasks;
    using FileSystem;
    using Instances;
    using ReportCard;
    using ServiceControl.LicenseManagement;
    using ServiceControlInstaller.Engine.Configuration.ServiceControl;
    using Validation;

    public class UnattendAuditInstaller
    {
        public UnattendAuditInstaller(ILogging loggingInstance)
        {
            logger = new Logging(loggingInstance);
            ZipInfo = new PlatformZipInfo(Constants.ServiceControlAuditExe, "ServiceControl Audit", "Particular.ServiceControl.Audit.zip");
        }

        public PlatformZipInfo ZipInfo { get; }

        public async Task<bool> Add(ServiceControlAuditNewInstance details, Func<PathInfo, Task<bool>> promptToProceed)
        {
            ZipInfo.ValidateZip();

            var checkLicenseResult = CheckLicenseIsValid();
            if (!checkLicenseResult.Valid)
            {
                logger.Error($"Install aborted - {checkLicenseResult.Message}");
                return false;
            }

            var instanceInstaller = details;
            instanceInstaller.ReportCard = new ReportCard();
            instanceInstaller.Version = Constants.CurrentVersion;

            //Validation
            await instanceInstaller.Validate(promptToProceed).ConfigureAwait(false);
            if (instanceInstaller.ReportCard.HasErrors)
            {
                foreach (var error in instanceInstaller.ReportCard.Errors)
                {
                    logger.Error(error);
                }

                return false;
            }

            try
            {
                instanceInstaller.CopyFiles(ZipInfo.ResourceName);
                instanceInstaller.WriteConfigurationFile();

                try
                {
                    instanceInstaller.RegisterUrlAcl();
                    instanceInstaller.SetupInstance();
                    instanceInstaller.RegisterService();
                }
                catch (Exception ex)
                {
                    instanceInstaller.ReportCard.Errors.Add(ex.Message);
                }

                foreach (var warning in instanceInstaller.ReportCard.Warnings)
                {
                    logger.Warn(warning);
                }

                if (instanceInstaller.ReportCard.HasErrors)
                {
                    foreach (var error in instanceInstaller.ReportCard.Errors)
                    {
                        logger.Error(error);
                    }

                    instanceInstaller.RemoveUrlAcl();

                    return false;
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex.Message);
                return false;
            }

            //Post Installation
            var instance = InstanceFinder.FindServiceControlInstance(instanceInstaller.Name);
            if (!instance.TryStartService())
            {
                logger.Warn("The service failed to start");
            }

            return true;
        }

        public bool Upgrade(ServiceControlAuditInstance instance, bool force)
        {
            if (force)
            {
                instance.CreateDatabaseBackup();
                instance.PersistenceManifest = ServiceControlPersisters.GetAuditPersistence(StorageEngineNames.RavenDB);
            }

            var compatibleStorageEngine = instance.PersistenceManifest.Name == StorageEngineNames.RavenDB;

            if (!compatibleStorageEngine)
            {
                var upgradeGuide4To5Url = "https://docs.particular.net/servicecontrol/upgrades/4to5/";
                logger.Error($"Upgrade aborted. Please note that the storage format has changed and the {instance.PersistenceManifest.DisplayName} storage engine is no longer available. Upgrading requires a side-by-side deployment of both versions. Migration guidance is available in the version 4 to 5 upgrade guidance at {upgradeGuide4To5Url}");
                return false;
            }

            var upgradeInfo = UpgradeInfo.GetUpgradePathFor(instance.Version);
            if (upgradeInfo.HasIncompatibleVersion)
            {
                var nextVersion = upgradeInfo.UpgradePath[0];
                logger.Error($"Upgrade aborted. An interim upgrade to version(s) {upgradeInfo} is required before upgrading to version {Constants.CurrentVersion}. Download available at https://github.com/Particular/ServiceControl/releases/tag/{nextVersion}");
                return false;
            }

            ZipInfo.ValidateZip();

            var checkLicenseResult = CheckLicenseIsValid();
            if (!checkLicenseResult.Valid)
            {
                logger.Error($"Upgrade aborted - {checkLicenseResult.Message}");
                return false;
            }

            instance.ReportCard = new ReportCard();

            var restartService = instance.Service.Status == ServiceControllerStatus.Running;
            if (!instance.TryStopService())
            {
                logger.Error("Service failed to stop or service stop timed out");
                return false;
            }

            try
            {
                instance.UpgradeTransportSeam();

                var backupFile = instance.BackupAppConfig();
                try
                {
                    instance.UpgradeFiles(ZipInfo.ResourceName);
                }
                finally
                {
                    instance.RestoreAppConfig(backupFile);
                }

                instance.SetupInstance();

                if (instance.ReportCard.HasErrors)
                {
                    foreach (var error in instance.ReportCard.Errors)
                    {
                        logger.Error(error);
                    }

                    return false;
                }

                if (restartService && !instance.TryStartService())
                {
                    logger.Error("Service failed to start after update - please check configuration for {0}", instance.Name);
                    return false;
                }
            }
            catch (Exception ex)
            {
                logger.Error("Upgrade Failed: {0}", ex.Message);
                return false;
            }

            return true;
        }

        public bool Delete(string instanceName, bool removeDatabase, bool removeLogs)
        {
            var instance = InstanceFinder.FindServiceControlInstance(instanceName);
            instance.ReportCard = new ReportCard();
            if (!instance.TryStopService())
            {
                logger.Error("Service failed to stop");
                return false;
            }

            try
            {
                instance.BackupAppConfig();
                instance.Service.SetStartupMode("Disabled");
                instance.Service.Delete();
                instance.RemoveUrlAcl();
                instance.RemoveBinFolder();
                if (removeLogs)
                {
                    instance.RemoveLogsFolder();
                }

                if (removeDatabase)
                {
                    instance.RemoveDataBaseFolder();
                }

                foreach (var warning in instance.ReportCard.Warnings)
                {
                    logger.Warn(warning);
                }

                if (instance.ReportCard.HasErrors)
                {
                    foreach (var error in instance.ReportCard.Errors)
                    {
                        logger.Error(error);
                    }

                    return false;
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex.Message);
                return false;
            }

            return true;
        }

        internal CheckLicenseResult CheckLicenseIsValid()
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
                return new CheckLicenseResult(false, "License does not cover this release of ServiceControl. Upgrade protection expired.");
            }

            return new CheckLicenseResult(true);
        }

        Logging logger;

        internal class CheckLicenseResult
        {
            public CheckLicenseResult(bool valid, string message = null)
            {
                Valid = valid;
                Message = message;
            }

            public bool Valid { get; }
            public string Message { get; }
        }
    }
}