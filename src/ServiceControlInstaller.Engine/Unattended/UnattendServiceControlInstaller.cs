namespace ServiceControlInstaller.Engine.Unattended
{
    using System;
    using System.IO;
    using System.Linq;
    using System.ServiceProcess;
    using Configuration.ServiceControl;
    using FileSystem;
    using Instances;
    using ServiceControl.LicenseManagement;
    using ReportCard;
    using Validation;

    public class UnattendServiceControlInstaller
    {
        public UnattendServiceControlInstaller(ILogging loggingInstance)
        {
            logger = new Logging(loggingInstance);
            var sourceroot = Path.GetFullPath(Environment.ExpandEnvironmentVariables("."));
            ZipInfo = ServiceControlZipInfo.Find(sourceroot);
        }

        public UnattendServiceControlInstaller(ILogging loggingInstance, string deploymentCachePath)
        {
            logger = new Logging(loggingInstance);
            var sourceroot = Path.GetFullPath(Environment.ExpandEnvironmentVariables(deploymentCachePath));
            ZipInfo = ServiceControlZipInfo.Find(sourceroot);
        }

        public PlatformZipInfo ZipInfo { get; }

        public bool Add(ServiceControlNewInstance details, Func<PathInfo, bool> promptToProceed)
        {
            ZipInfo.ValidateZip();

            var checkLicenseResult = CheckLicenseIsValid();
            if (!checkLicenseResult.Valid)
            {
                logger.Error($"Install aborted - {checkLicenseResult.Message}");
                return false;
            }

            var instanceInstaller = details;
            
            try
            {
                instanceInstaller.ReportCard = new ReportCard();
                instanceInstaller.Version = ZipInfo.Version;

                //Validation
                instanceInstaller.Validate(promptToProceed);

                if (instanceInstaller.ReportCard.HasErrors)
                {
                    return false;
                }

                try
                {
                    instanceInstaller.CopyFiles(ZipInfo.FilePath);
                    instanceInstaller.WriteConfigurationFile();
                    instanceInstaller.RegisterUrlAcl();
                    instanceInstaller.SetupInstance();
                    instanceInstaller.RegisterService();

                    if (instanceInstaller.ReportCard.HasErrors)
                    {
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
            finally
            {
                WriteWarningsAndErrorsToLog(instanceInstaller);
            }
        }

        public bool Upgrade(ServiceControlInstance instance, ServiceControlUpgradeOptions options)
        {
            if (instance.Version < options.UpgradeInfo.CurrentMinimumVersion)
            {
                logger.Error($"Upgrade aborted. An interim upgrade to version {options.UpgradeInfo.RecommendedUpgradeVersion} is required before upgrading to version {ZipInfo.Version}. Download available at https://github.com/Particular/ServiceControl/releases/tag/{options.UpgradeInfo.RecommendedUpgradeVersion}");
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
                    instance.UpgradeFiles(ZipInfo.FilePath);
                }
                finally
                {
                    instance.RestoreAppConfig(backupFile);
                }

                options.ApplyChangesToInstance(instance);

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

        internal bool Update(ServiceControlInstance instance, bool startService)
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

        public bool Delete(string instanceName, bool removeDB, bool removeLogs)
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

                if (removeDB)
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

            if (ZipInfo.TryReadReleaseDate(out var releaseDate))
            {
                if (license.Details.ReleaseNotCoveredByMaintenance(releaseDate))
                {
                    return new CheckLicenseResult(false, "License does not cover this release of ServiceControl.Upgrade protection expired");
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

            public bool Valid { get; }
            public string Message { get; }
        }

        public bool AddRemoteInstance(ServiceControlInstance instance, string[] remoteInstanceAddresses, ILogging log)
        {
            if (Compatibility.RemoteInstancesDoNotNeedQueueAddress.SupportedFrom > instance.Version)
            {
                log.Error($"Cannot add remote instances to instances older than {Compatibility.RemoteInstancesDoNotNeedQueueAddress.SupportedFrom}");
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
                foreach (var remoteInstanceAddress in remoteInstanceAddresses)
                {
                    instance.AddRemoteInstance(remoteInstanceAddress);
                }

                instance.ApplyConfigChange();

                if (restartService && !instance.TryStartService())
                {
                    logger.Error("Service failed to start after adding remote instances - please check configuration for {0}", instance.Name);
                    return false;
                }
            }
            catch (Exception ex)
            {
                logger.Error("Adding remote instances Failed: {0}", ex.Message);
                return false;
            }

            return true;
        }

        public bool RemoveRemoteInstance(ServiceControlInstance instance, string[] remoteInstanceAddresses, ILogging log)
        {
            instance.ReportCard = new ReportCard();

            var restartService = instance.Service.Status == ServiceControllerStatus.Running;
            if (!instance.TryStopService())
            {
                logger.Error("Service failed to stop or service stop timed out");
            }

            try
            {
                instance.RemoteInstances.RemoveAll(x => remoteInstanceAddresses.Contains(x.ApiUri, StringComparer.InvariantCultureIgnoreCase));

                instance.ApplyConfigChange();

                if (restartService && !instance.TryStartService())
                {
                    logger.Error("Service failed to start after removing remote instances - please check configuration for {0}", instance.Name);
                    return false;
                }
            }
            catch (Exception ex)
            {
                logger.Error("Removing remote instances Failed: {0}", ex.Message);
                return false;
            }

            return true;
        }
        
        void WriteWarningsAndErrorsToLog(ServiceControlNewInstance instanceInstaller)
        {
            foreach (var warning in instanceInstaller.ReportCard.Warnings)
            {
                logger.Warn(warning);
            }

            foreach (var error in instanceInstaller.ReportCard.Errors)
            {
                logger.Error(error);
            }
        }
    }
}
