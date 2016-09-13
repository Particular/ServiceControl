// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedMethodReturnValue.Global
namespace ServiceControlInstaller.Engine.Unattended
{
    using System;
    using System.IO;
    using System.ServiceProcess;
    using ServiceControlInstaller.Engine.FileSystem;
    using ServiceControlInstaller.Engine.Instances;
    using ServiceControlInstaller.Engine.ReportCard;
    using ServiceControlInstaller.Engine.Validation;

    public class UnattendInstaller
    {
        Logging logger;

        public ServiceControlZipInfo ZipInfo { get; }

        public UnattendInstaller(ILogging loggingInstance)
        {
            logger = new Logging(loggingInstance);
            var sourceroot = Path.GetFullPath(Environment.ExpandEnvironmentVariables("."));
            ZipInfo = ServiceControlZipInfo.Find(sourceroot);
        }

        public UnattendInstaller(ILogging loggingInstance, string deploymentCachePath)
        {
            logger = new Logging(loggingInstance);
            var sourceroot = Path.GetFullPath(Environment.ExpandEnvironmentVariables(deploymentCachePath));
            ZipInfo = ServiceControlZipInfo.Find(sourceroot);
        }

        public bool Add(ServiceControlInstanceMetadata details, Func<PathInfo, bool> promptToProceed)
        {
            ZipInfo.ValidateZip();

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
                logger.Info("Unpacking files...");
                instanceInstaller.CopyFiles(ZipInfo.FilePath);
                logger.Info("Writing configuration file...");
                instanceInstaller.WriteConfigurationFile();
                logger.Info("Registering UrlAcl...");
                instanceInstaller.RegisterUrlAcl();
                logger.Info("Running setup to create queues...");
                instanceInstaller.SetupInstance();
                logger.Info("Registering Windows Service...");
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
            var instance = ServiceControlInstance.FindByName(instanceInstaller.Name);
            if (!instance.TryStartService())
            {
                logger.Warn("The service failed to start");
            }
            return true;
        }

        public bool Upgrade(ServiceControlInstance instance, InstanceUpgradeOptions options)
        {
            instance.ReportCard = new ReportCard();
            ZipInfo.ValidateZip();
            var restartService = instance.Service.Status == ServiceControllerStatus.Running;

            if (!instance.TryStopService())
            {
                logger.Error("Service failed to stop or service stop timed out");
                return false;
            }
            try
            {
                var backupFile = instance.BackupAppConfig();
                try
                {
                    if (options.BackupRavenDbBeforeUpgrade)
                    {
                        var backup = new UpgradeBackupManager(instance, ZipInfo.FilePath, options.BackupPath);
                        backup.EnterBackupMode();
                        try
                        {   if (!backup.BackupDatabase())
                            {
                                return false;
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.Error($"Backup failed - {ex.Message}");
                            instance.ReportCard.Errors.Add($"An exception occurred while attempting the DB backup - {ex.Message}");
                            return false;
                        }
                        finally
                        {
                            instance.TryStopService();
                            backup.ExitBackupMode();
                        }
                    }
                    logger.Info("Unpacking files...");
                    instance.UpgradeFiles(ZipInfo.FilePath);
                    if (!string.IsNullOrWhiteSpace(options.BodyStoragePath))
                    {
                        instance.BodyStoragePath = options.BodyStoragePath;
                    }
                    if (!string.IsNullOrWhiteSpace(options.IngestionCachePath))
                    {
                        instance.IngestionCachePath = options.IngestionCachePath;
                    }
                    logger.Info("Moving DB files...");
                    instance.MoveRavenDatabase(instance.DBPath);
                    instance.EnsureDirectoriesExist();
                }
                finally
                {
                    instance.RestoreAppConfig(backupFile);
                }
                logger.Info("Applying config changes...");
                options.ApplyChangesToInstance(instance);

                logger.Info("Running setup to create queues");
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
            catch(Exception ex)
            {
                logger.Error("Update failed: {0}",  ex.Message);
                return false;
            }
            return true;
        }

        // ReSharper disable once UnusedMethodReturnValue.Global
        public bool Delete(string instanceName, bool removeDB, bool removeLogs)
        {
            var instance = ServiceControlInstance.FindByName(instanceName);
            instance.ReportCard = new ReportCard();
            if (!instance.TryStopService())
            {
                logger.Error("Service failed to stop");
                return false;
            }

            try
            {
                instance.BackupAppConfig();
                logger.Info("Disable Windows Service...");

                instance.Service.SetStartupMode("Disabled");
                logger.Info("Delete Windows Service...");
                instance.Service.Delete();
                logger.Info("Remove UrlAcL...");
                instance.RemoveUrlAcl();
                logger.Info("Remove Binaries...");
                instance.RemoveBinFolder(); 
                if (removeLogs)
                {
                    logger.Info("Remove Logs Folder...");
                    instance.RemoveLogsFolder();
                }
                else
                {
                    logger.Info($"Skipped removing Logs Folder ({instance.LogPath}) ...");
                }
                if (removeDB)
                {
                    logger.Info("Removing Data...");
                    instance.RemoveDataBaseFolder();
                    instance.RemoveIngestionFolder();
                    instance.RemoveBodyStorageFolder();
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
            catch(Exception ex)
            {
                logger.Error(ex.Message);
                return false;
            }
            return true;
        }
    }
}

