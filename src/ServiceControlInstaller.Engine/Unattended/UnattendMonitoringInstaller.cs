// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedMethodReturnValue.Global

namespace ServiceControlInstaller.Engine.Unattended
{
    using System;
    using System.IO;
    using System.ServiceProcess;
    using FileSystem;
    using Instances;
    using LicenseMgmt;
    using ReportCard;
    using Validation;

    public class UnattendMonitoringInstaller
    {
        public UnattendMonitoringInstaller(ILogging loggingInstance)
        {
            logger = new Logging(loggingInstance);
            var sourceroot = Path.GetFullPath(Environment.ExpandEnvironmentVariables("."));
            ZipInfo = MonitoringZipInfo.Find(sourceroot);
        }

        public UnattendMonitoringInstaller(ILogging loggingInstance, string deploymentCachePath)
        {
            logger = new Logging(loggingInstance);
            var sourceroot = Path.GetFullPath(Environment.ExpandEnvironmentVariables(deploymentCachePath));
            ZipInfo = MonitoringZipInfo.Find(sourceroot);
        }

        public MonitoringZipInfo ZipInfo { get; }

        public bool Add(MonitoringNewInstance details, Func<PathInfo, bool> promptToProceed)
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

            //Validation
            instanceInstaller.Validate(promptToProceed);
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
                instanceInstaller.CopyFiles(ZipInfo.FilePath);
                instanceInstaller.WriteConfigurationFile();
                instanceInstaller.RegisterUrlAcl();
                instanceInstaller.SetupInstance();
                instanceInstaller.RegisterService();
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
            }

            try
            {
                var backupFile = instance.BackupAppConfig();
                try
                {
                    instance.UpgradeFiles(ZipInfo.FilePath);
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

        internal bool Update(MonitoringInstance instance, bool startService)
        {
            instance.ReportCard = new ReportCard();
            instance.ValidateChanges();
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

        // ReSharper disable once UnusedMethodReturnValue.Global
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

            if (ZipInfo.TryReadMonitoringReleaseDate(out var releaseDate))
            {
                if (license.Details.ReleaseNotCoveredByMaintenance(releaseDate))
                {
                    return new CheckLicenseResult(false, "License does not cover this release of Monitoring. Upgrade protection expired");
                }
            }
            else
            {
                throw new Exception("Failed to retrieve release date for new version");
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

            public bool Valid { get; private set; }
            public string Message { get; private set; }
        }
    }
}