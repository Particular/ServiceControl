namespace ServiceControlInstaller.Engine.Unattended
{
    using System;
    using System.ServiceProcess;
    using System.Threading.Tasks;
    using FileSystem;
    using Instances;
    using ReportCard;
    using Validation;

    public class UnattendMonitoringInstaller
    {
        public UnattendMonitoringInstaller(ILogging loggingInstance)
        {
            logger = new Logging(loggingInstance);
            ZipInfo = new PlatformZipInfo(Constants.MonitoringExe, "ServiceControl Monitoring", "Particular.ServiceControl.Monitoring.zip");
        }

        public PlatformZipInfo ZipInfo { get; }

        public async Task<bool> Add(MonitoringNewInstance details, Func<PathInfo, Task<bool>> promptToProceed)
        {
            ZipInfo.ValidateZip();

            var instanceInstaller = details;
            instanceInstaller.ReportCard = new ReportCard();

            //Validation
            await instanceInstaller.Validate(promptToProceed);
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
            var instance = InstanceFinder.FindMonitoringInstance(instanceInstaller.Name);
            if (!instance.TryStartService())
            {
                logger.Warn("The service failed to start");
            }

            return true;
        }

        public bool Upgrade(MonitoringInstance instance)
        {
            ZipInfo.ValidateZip();

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

        internal async Task<bool> Update(MonitoringInstance instance, bool startService)
        {
            instance.ReportCard = new ReportCard();
            await instance.ValidateChanges();
            if (instance.ReportCard.HasErrors)
            {
                foreach (var error in instance.ReportCard.Errors)
                {
                    logger.Error(error);
                }

                return false;
            }

            try
            {
                if (!instance.TryStopService())
                {
                    logger.Error("Service failed to stop");
                    return false;
                }

                instance.ApplyConfigChange();
                if (instance.ReportCard.HasErrors)
                {
                    foreach (var error in instance.ReportCard.Errors)
                    {
                        logger.Error(error);
                    }

                    return false;
                }

                if (startService && !instance.TryStartService())
                {
                    logger.Error("Service failed to start after changes - please check configuration for {0}", instance.Name);
                    return false;
                }
            }
            catch (Exception ex)
            {
                logger.Error("Update failed: {0}", ex.Message);
                return false;
            }

            return true;
        }

        public bool Delete(string instanceName, bool removeLogs)
        {
            var instance = InstanceFinder.FindMonitoringInstance(instanceName);
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