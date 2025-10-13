namespace ServiceControlInstaller.Engine.Unattended
{
    using System;
    using System.ServiceProcess;
    using System.Threading.Tasks;
    using FileSystem;
    using Instances;
    using ReportCard;
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
            ZipInfo.ValidateZip();

            instance.ReportCard = new ReportCard();

            var restartService = instance.Service.Status == ServiceControllerStatus.Running;
            if (!instance.TryStopService())
            {
                logger.Error("Service failed to stop or service stop timed out");
                return false;
            }

            if (force)
            {
                instance.CreateDatabaseBackup();
                instance.PersistenceManifest = ServiceControlPersisters.GetAuditPersistence(StorageEngineNames.RavenDB);
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

        Logging logger;
    }
}