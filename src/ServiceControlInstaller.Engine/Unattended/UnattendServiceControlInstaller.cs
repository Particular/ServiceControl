﻿namespace ServiceControlInstaller.Engine.Unattended
{
    using System;
    using System.Linq;
    using System.ServiceProcess;
    using System.Threading.Tasks;
    using Configuration.ServiceControl;
    using FileSystem;
    using Instances;
    using ReportCard;
    using Validation;

    public class UnattendServiceControlInstaller
    {
        public UnattendServiceControlInstaller(ILogging loggingInstance)
        {
            logger = new Logging(loggingInstance);
            ZipInfo = new PlatformZipInfo(Constants.ServiceControlExe, "ServiceControl", "Particular.ServiceControl.zip");
        }

        public PlatformZipInfo ZipInfo { get; }

        public async Task<bool> Add(ServiceControlNewInstance details, Func<PathInfo, Task<bool>> promptToProceed)
        {
            ZipInfo.ValidateZip();

            var instanceInstaller = details;

            try
            {
                instanceInstaller.ReportCard = new ReportCard();
                instanceInstaller.Version = Constants.CurrentVersion;

                //Validation
                await instanceInstaller.Validate(promptToProceed).ConfigureAwait(false);

                if (instanceInstaller.ReportCard.HasErrors)
                {
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

                    if (instanceInstaller.ReportCard.HasErrors)
                    {
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
            finally
            {
                WriteWarningsAndErrorsToLog(instanceInstaller.ReportCard);
            }
        }

        public bool Upgrade(ServiceControlInstance instance, ServiceControlUpgradeOptions options)
        {
            ZipInfo.ValidateZip();

            instance.ReportCard = new ReportCard();

            var restartService = instance.Service.Status == ServiceControllerStatus.Running;
            if (!instance.TryStopService())
            {
                logger.Error("Service failed to stop or service stop timed out");
                return false;
            }

            if (options.Force)
            {
                instance.CreateDatabaseBackup();
                instance.PersistenceManifest = ServiceControlPersisters.GetPrimaryPersistence(StorageEngineNames.RavenDB);
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

                options.ApplyChangesToInstance(instance);

                instance.SetupInstance();

                if (instance.ReportCard.HasErrors)
                {
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
            finally
            {
                WriteWarningsAndErrorsToLog(instance.ReportCard);
            }

            return true;
        }

        internal async Task<bool> Update(ServiceControlInstance instance, bool startService)
        {
            instance.ReportCard = new ReportCard();
            await instance.ValidateChanges().ConfigureAwait(false);
            if (instance.ReportCard.HasErrors)
            {
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
                    return false;
                }

                if (startService && !instance.TryStartService())
                {
                    logger.Error("Service failed to start after changes - please check configuration for {0}", instance.Name);
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                logger.Error("Update failed: {0}", ex.Message);
                return false;
            }
            finally
            {
                WriteWarningsAndErrorsToLog(instance.ReportCard);
            }
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

                if (instance.ReportCard.HasErrors)
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex.Message);
                return false;
            }
            finally
            {
                WriteWarningsAndErrorsToLog(instance.ReportCard);
            }

            return true;
        }

        Logging logger;

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

                if (instance.ReportCard.HasErrors)
                {
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                logger.Error("Adding remote instances Failed: {0}", ex.Message);
                return false;
            }
            finally
            {
                WriteWarningsAndErrorsToLog(instance.ReportCard);
            }
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

                if (instance.ReportCard.HasErrors)
                {
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                logger.Error("Removing remote instances Failed: {0}", ex.Message);
                return false;
            }
            finally
            {
                WriteWarningsAndErrorsToLog(instance.ReportCard);
            }
        }

        void WriteWarningsAndErrorsToLog(ReportCard reportCard)
        {
            foreach (var warning in reportCard.Warnings)
            {
                logger.Warn(warning);
            }

            foreach (var error in reportCard.Errors)
            {
                logger.Error(error);
            }
        }
    }
}
