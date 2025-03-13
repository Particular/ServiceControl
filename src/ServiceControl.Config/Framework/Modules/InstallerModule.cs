namespace ServiceControl.Config.Framework.Modules
{
    using System;
    using System.Threading.Tasks;
    using Autofac;
    using ServiceControlInstaller.Engine.FileSystem;
    using ServiceControlInstaller.Engine.Instances;
    using ServiceControlInstaller.Engine.ReportCard;
    using ServiceControlInstaller.Engine.Validation;
    using Constants = ServiceControlInstaller.Engine.Instances.Constants;
    using Module = Autofac.Module;

    public class InstallerModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<ServiceControlInstanceInstaller>().SingleInstance();
            builder.RegisterType<MonitoringInstanceInstaller>().SingleInstance();
            builder.RegisterType<ServiceControlAuditInstanceInstaller>().SingleInstance();
        }
    }

    public class ServiceControlInstanceInstaller : ServiceControlInstallerBase
    {
        public ServiceControlInstanceInstaller()
        {
            ZipInfo = new PlatformZipInfo(Constants.ServiceControlExe, "ServiceControl", "Particular.ServiceControl.zip");
        }
    }

    public class ServiceControlAuditInstanceInstaller : ServiceControlInstallerBase
    {
        public ServiceControlAuditInstanceInstaller()
        {
            ZipInfo = new PlatformZipInfo(Constants.ServiceControlAuditExe, "ServiceControl Audit", "Particular.ServiceControl.Audit.zip");
        }
    }

    public abstract class InstallerBase
    {
        public PlatformZipInfo ZipInfo { get; init; }
    }

    public class ServiceControlInstallerBase : InstallerBase
    {
        internal async Task<ReportCard> Add(ServiceControlInstallableBase details, IProgress<ProgressDetails> progress, Func<PathInfo, Task<bool>> promptToProceed)
        {
            ZipInfo.ValidateZip();

            var instanceInstaller = details;
            instanceInstaller.ReportCard = new ReportCard();

            //Validation
            await instanceInstaller.Validate(promptToProceed);
            if (instanceInstaller.ReportCard.HasErrors || instanceInstaller.ReportCard.CancelRequested)
            {
                instanceInstaller.ReportCard.Status = Status.FailedValidation;
                return instanceInstaller.ReportCard;
            }

            progress.Report(3, 9, "Copying files...");
            instanceInstaller.CopyFiles(ZipInfo.ResourceName);
            progress.Report(4, 9, "Writing configurations...");
            instanceInstaller.WriteConfigurationFile();

            try
            {
                progress.Report(5, 9, "Registering URL ACLs...");
                instanceInstaller.RegisterUrlAcl();
                progress.Report(6, 9, "Instance setup in progress, this could take several minutes...");
                instanceInstaller.SetupInstance();
            }
            catch (Exception ex)
            {
                instanceInstaller.ReportCard.Errors.Add(ex.Message);
            }

            if (!instanceInstaller.ReportCard.HasErrors)
            {
                progress.Report(7, 9, "Registering service...");
                instanceInstaller.RegisterService();
                //Post Installation
                progress.Report(8, 9, "Starting service...");
                var instance = InstanceFinder.FindServiceControlInstance(instanceInstaller.Name);
                if (!instance.TryStartService())
                {
                    instanceInstaller.ReportCard.Warnings.Add($"New instance did not startup - please check configuration for {instance.Name}");
                }
            }
            else
            {
                instanceInstaller.RemoveUrlAcl();
            }

            instanceInstaller.ReportCard.SetStatus();
            return instanceInstaller.ReportCard;
        }

        internal ReportCard Upgrade(ServiceControlBaseService instance, ServiceControlUpgradeOptions upgradeOptions, IProgress<ProgressDetails> progress = null)
        {
            progress ??= new Progress<ProgressDetails>();

            instance.ReportCard = new ReportCard();
            ZipInfo.ValidateZip();

            var totalSteps = 5;
            var currentStep = 0;

            progress.Report(currentStep++, totalSteps, "Stopping instance...");
            if (!instance.TryStopService())
            {
                return new ReportCard
                {
                    Errors = { "Service failed to stop" },
                    Status = Status.Failed
                };
            }

            instance.UpgradeTransportSeam();

            progress.Report(currentStep++, totalSteps, "Backing up app.config...");
            var backupFile = instance.BackupAppConfig();
            try
            {
                progress.Report(currentStep++, totalSteps, "Upgrading Files...");
                instance.UpgradeFiles(ZipInfo.ResourceName);
            }
            catch (Exception e)
            {
                return new ReportCard
                {
                    Errors = { e.Message },
                    Status = Status.Failed
                };
            }
            finally
            {
                progress.Report(currentStep++, totalSteps, "Restoring app.config...");
                instance.RestoreAppConfig(backupFile);

                var restoredConnectionString = instance.AppConfig.Config.ConnectionStrings.ConnectionStrings["NServiceBus/Transport"];

                if (restoredConnectionString is not null &&
                    !string.Equals(instance.ConnectionString, restoredConnectionString.ConnectionString, StringComparison.OrdinalIgnoreCase))
                {
                    upgradeOptions.UpgradedConnectionString = restoredConnectionString.ConnectionString;
                }
            }

            UpgradeOptions(upgradeOptions, instance);

            progress.Report(++currentStep, totalSteps, "Instance setup in progress, this could take several minutes...");
            instance.SetupInstance();

            instance.ReportCard.SetStatus();
            return instance.ReportCard;
        }

        protected virtual void UpgradeOptions(ServiceControlUpgradeOptions upgradeOptions, ServiceControlBaseService instance)
        {
            upgradeOptions.ApplyChangesToInstance(instance);
        }

        internal async Task<ReportCard> Update(ServiceControlBaseService instance, bool startService)
        {
            try
            {
                instance.ReportCard = new ReportCard();
                await instance.ValidateChanges();
                if (instance.ReportCard.HasErrors)
                {
                    instance.ReportCard.Status = Status.FailedValidation;
                    return instance.ReportCard;
                }

                if (!instance.TryStopService())
                {
                    instance.ReportCard.Errors.Add("Service failed to stop");
                    instance.ReportCard.Status = Status.Failed;
                    return instance.ReportCard;
                }

                instance.ApplyConfigChange();
                if (!instance.ReportCard.HasErrors)
                {
                    if (startService && !instance.TryStartService())
                    {
                        instance.ReportCard.Warnings.Add($"Service did not start after changes - please check configuration for {instance.Name}");
                    }
                }

                instance.ReportCard.SetStatus();
                return instance.ReportCard;
            }
            finally
            {
                instance.Reload();
            }
        }

        internal ReportCard Delete(string instanceName, bool removeDB, bool removeLogs, IProgress<ProgressDetails> progress = null)
        {
            progress ??= new Progress<ProgressDetails>();
            progress.Report(0, 7, "Stopping instance...");
            var instance = InstanceFinder.FindServiceControlInstance(instanceName);
            instance.ReportCard = new ReportCard();

            if (!instance.TryStopService())
            {
                return new ReportCard
                {
                    Errors = { "Service failed to stop" },
                    Status = Status.Failed
                };
            }

            instance.BackupAppConfig();

            progress.Report(1, 7, "Disabling startup...");
            instance.Service.SetStartupMode("Disabled");

            progress.Report(2, 7, "Deleting service...");
            instance.Service.Delete();

            progress.Report(3, 7, "Removing URL ACL...");
            instance.RemoveUrlAcl();

            progress.Report(4, 7, "Deleting install...");
            instance.RemoveBinFolder();

            if (removeLogs)
            {
                progress.Report(5, 7, "Deleting logs...");
                instance.RemoveLogsFolder();
            }

            if (removeDB)
            {
                progress.Report(6, 7, "Deleting database...");
                instance.RemoveDataBaseFolder();
            }

            progress.Report(new ProgressDetails());

            instance.ReportCard.SetStatus();
            return instance.ReportCard;
        }
    }

    public class MonitoringInstanceInstaller : InstallerBase
    {
        public MonitoringInstanceInstaller()
        {
            ZipInfo = new PlatformZipInfo(Constants.MonitoringExe, "ServiceControl Monitoring", "Particular.ServiceControl.Monitoring.zip");
        }

        internal async Task<ReportCard> Add(MonitoringNewInstance details, IProgress<ProgressDetails> progress, Func<PathInfo, Task<bool>> promptToProceed)
        {
            ZipInfo.ValidateZip();

            var instanceInstaller = details;
            instanceInstaller.ReportCard = new ReportCard();

            //Validation
            await instanceInstaller.Validate(promptToProceed);
            if (instanceInstaller.ReportCard.HasErrors || instanceInstaller.ReportCard.CancelRequested)
            {
                instanceInstaller.ReportCard.Status = Status.FailedValidation;
                return instanceInstaller.ReportCard;
            }

            progress.Report(3, 9, "Copying files...");
            instanceInstaller.CopyFiles(ZipInfo.ResourceName);
            progress.Report(4, 9, "Writing configurations...");
            instanceInstaller.WriteConfigurationFile();

            try
            {
                progress.Report(5, 9, "Registering URL ACLs...");
                instanceInstaller.RegisterUrlAcl();
                progress.Report(6, 9, "Creating queues...");
                instanceInstaller.SetupInstance();
            }
            catch (Exception ex)
            {
                instanceInstaller.ReportCard.Errors.Add(ex.Message);
            }

            if (!instanceInstaller.ReportCard.HasErrors)
            {
                progress.Report(7, 9, "Registering service...");
                instanceInstaller.RegisterService();
                //Post Installation
                progress.Report(8, 9, "Starting service...");
                var instance = InstanceFinder.FindMonitoringInstance(instanceInstaller.Name);
                if (!instance.TryStartService())
                {
                    instanceInstaller.ReportCard.Warnings.Add($"New instance did not startup - please check configuration for {instance.Name}");
                }
            }
            else
            {
                instanceInstaller.RemoveUrlAcl();
            }

            instanceInstaller.ReportCard.SetStatus();
            return instanceInstaller.ReportCard;
        }

        internal ReportCard Upgrade(string instanceName, IProgress<ProgressDetails> progress = null)
        {
            progress ??= new Progress<ProgressDetails>();

            var instance = InstanceFinder.FindMonitoringInstance(instanceName);
            instance.ReportCard = new ReportCard();
            ZipInfo.ValidateZip();

            progress.Report(0, 5, "Stopping instance...");
            if (!instance.TryStopService())
            {
                return new ReportCard
                {
                    Errors = { "Service failed to stop" },
                    Status = Status.Failed
                };
            }

            instance.UpgradeTransportSeam();

            progress.Report(1, 5, "Backing up app.config...");
            var backupFile = instance.BackupAppConfig();
            try
            {
                progress.Report(2, 5, "Upgrading Files...");
                instance.UpgradeFiles(ZipInfo.ResourceName);
            }
            catch (Exception e)
            {
                return new ReportCard
                {
                    Errors = { e.Message },
                    Status = Status.Failed
                };
            }
            finally
            {
                progress.Report(3, 5, "Restoring app.config...");
                instance.RestoreAppConfig(backupFile);
            }

            progress.Report(4, 5, "Running Queue Creation...");
            instance.SetupInstance();
            instance.ReportCard.SetStatus();
            return instance.ReportCard;
        }

        internal async Task<ReportCard> Update(MonitoringInstance instance, bool startService)
        {
            try
            {
                instance.ReportCard = new ReportCard();
                await instance.ValidateChanges();
                if (instance.ReportCard.HasErrors)
                {
                    instance.ReportCard.Status = Status.FailedValidation;
                    return instance.ReportCard;
                }

                if (!instance.TryStopService())
                {
                    instance.ReportCard.Errors.Add("Service failed to stop");
                    instance.ReportCard.Status = Status.Failed;
                    return instance.ReportCard;
                }

                instance.ApplyConfigChange();
                if (!instance.ReportCard.HasErrors)
                {
                    if (startService && !instance.TryStartService())
                    {
                        instance.ReportCard.Warnings.Add($"Service did not start after changes - please check configuration for {instance.Name}");
                    }
                }

                instance.ReportCard.SetStatus();
                return instance.ReportCard;
            }
            finally
            {
                instance.Reload();
            }
        }

        internal ReportCard Delete(string instanceName, bool removeLogs, IProgress<ProgressDetails> progress = null)
        {
            progress ??= new Progress<ProgressDetails>();
            progress.Report(0, 7, "Stopping instance...");
            var instance = InstanceFinder.FindMonitoringInstance(instanceName);
            instance.ReportCard = new ReportCard();

            if (!instance.TryStopService())
            {
                return new ReportCard
                {
                    Errors = { "Service failed to stop" },
                    Status = Status.Failed
                };
            }

            instance.BackupAppConfig();

            progress.Report(1, 7, "Disabling startup...");
            instance.Service.SetStartupMode("Disabled");

            progress.Report(2, 7, "Deleting service...");
            instance.Service.Delete();

            progress.Report(3, 7, "Removing URL ACL...");
            instance.RemoveUrlAcl();

            progress.Report(4, 7, "Deleting install...");
            instance.RemoveBinFolder();

            if (removeLogs)
            {
                progress.Report(5, 7, "Deleting logs...");
                instance.RemoveLogsFolder();
            }

            progress.Report(new ProgressDetails());

            instance.ReportCard.SetStatus();
            return instance.ReportCard;
        }
    }
}