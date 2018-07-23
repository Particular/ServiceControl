namespace ServiceControl.Config.Framework.Modules
{
    using System;
    using System.IO;
    using System.Reflection;
    using Autofac;
    using ServiceControlInstaller.Engine.FileSystem;
    using ServiceControlInstaller.Engine.Instances;
    using ServiceControlInstaller.Engine.LicenseMgmt;
    using ServiceControlInstaller.Engine.ReportCard;
    using ServiceControlInstaller.Engine.Validation;
    using Module = Autofac.Module;

    public class InstallerModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<ServiceControlInstanceInstaller>().SingleInstance();
            builder.RegisterType<MonitoringInstanceInstaller>().SingleInstance();
        }
    }

    public class ServiceControlInstanceInstaller
    {
        public ServiceControlZipInfo ZipInfo { get; }

        public ServiceControlInstanceInstaller()
        {
            var appDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            ZipInfo = ServiceControlZipInfo.Find(appDirectory);
        }

        internal ReportCard Add(ServiceControlNewInstance details, IProgress<ProgressDetails> progress, Func<PathInfo, bool> promptToProceed)
        {
            ZipInfo.ValidateZip();

            var instanceInstaller = details;
            instanceInstaller.ReportCard = new ReportCard();

            //Validation
            instanceInstaller.Validate(promptToProceed);
            if (instanceInstaller.ReportCard.HasErrors || instanceInstaller.ReportCard.CancelRequested)
            {
                instanceInstaller.ReportCard.Status = Status.FailedValidation;
                return instanceInstaller.ReportCard;
            }

            progress.Report(3, 9, "Copying files...");
            instanceInstaller.CopyFiles(ZipInfo.FilePath);
            progress.Report(4, 9, "Writing configurations...");
            instanceInstaller.WriteConfigurationFile();
            progress.Report(5, 9, "Registering URL ACLs...");
            instanceInstaller.RegisterUrlAcl();
            progress.Report(6, 9, "Creating queues...");
            instanceInstaller.SetupInstance();

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
            instanceInstaller.ReportCard.SetStatus();
            return instanceInstaller.ReportCard;
        }

        internal ReportCard Upgrade(ServiceControlInstance instance, ServiceControlUpgradeOptions upgradeOptions, IProgress<ProgressDetails> progress = null)
        {
            progress = progress ?? new Progress<ProgressDetails>();

            instance.ReportCard = new ReportCard();
            ZipInfo.ValidateZip();

            var totalSteps = 7 - (upgradeOptions.UpgradeInfo.DeleteIndexes ? 0 : 1) - (upgradeOptions.UpgradeInfo.DataBaseUpdate ? 0 : 1);
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

            progress.Report(currentStep++, totalSteps, "Backing up app.config...");
            var backupFile = instance.BackupAppConfig();
            try
            {
                progress.Report(currentStep++, totalSteps, "Upgrading Files...");
                instance.UpgradeFiles(ZipInfo.FilePath);
            }
            finally
            {
                progress.Report(currentStep++, totalSteps, "Restoring app.config...");
                instance.RestoreAppConfig(backupFile);
            }

            upgradeOptions.ApplyChangesToInstance(instance);

            if (upgradeOptions.UpgradeInfo.DeleteIndexes)
            {
                progress.Report(currentStep++, totalSteps, "Removing database indexes...");
                instance.RemoveDatabaseIndexes();
            }

            if (upgradeOptions.UpgradeInfo.DataBaseUpdate)
            {
                progress.Report(currentStep, totalSteps, "Updating Database...");
                // ReSharper disable once AccessToModifiedClosure
                instance.UpdateDatabase(msg => progress.Report(currentStep, totalSteps, $"Updating Database...{Environment.NewLine}{msg}"));
            }

            progress.Report(++currentStep, totalSteps, "Running Queue Creation...");
            instance.SetupInstance();

            instance.ReportCard.SetStatus();
            return instance.ReportCard;
        }

        internal ReportCard Update(ServiceControlInstance instance, bool startService)
        {
            try
            {
                instance.ReportCard = new ReportCard();
                instance.ValidateChanges();
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
            progress = progress ?? new Progress<ProgressDetails>();
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

        internal CheckLicenseResult CheckLicenseIsValid()
        {
            DateTime releaseDate;
            var license = LicenseManager.FindLicense();
            if (license.Details.HasLicenseExpired())
            {
                return new CheckLicenseResult(false, "License has expired");
            }

            if (!license.Details.ValidForServiceControl)
            {
                return new CheckLicenseResult(false, "This license edition does not include ServiceControl");
            }

            if (ZipInfo.TryReadServiceControlReleaseDate(out releaseDate))
            {
                if (license.Details.ReleaseNotCoveredByMaintenance(releaseDate))
                {
                    return new CheckLicenseResult(false, "License does not cover this release of ServiceControl. Upgrade protection expired");
                }
            }
            else
            {
                throw new Exception("Failed to retrieve release date for new version");
            }
            return new CheckLicenseResult(true);
        }

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

    public class MonitoringInstanceInstaller
    {
        public MonitoringZipInfo ZipInfo { get; }

        public MonitoringInstanceInstaller()
        {
            var appDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            ZipInfo = MonitoringZipInfo.Find(appDirectory);
        }

        internal ReportCard Add(MonitoringNewInstance details, IProgress<ProgressDetails> progress, Func<PathInfo, bool> promptToProceed)
        {
            ZipInfo.ValidateZip();

            var instanceInstaller = details;
            instanceInstaller.ReportCard = new ReportCard();

            //Validation
            instanceInstaller.Validate(promptToProceed);
            if (instanceInstaller.ReportCard.HasErrors || instanceInstaller.ReportCard.CancelRequested)
            {
                instanceInstaller.ReportCard.Status = Status.FailedValidation;
                return instanceInstaller.ReportCard;
            }

            progress.Report(3, 9, "Copying files...");
            instanceInstaller.CopyFiles(ZipInfo.FilePath);
            progress.Report(4, 9, "Writing configurations...");
            instanceInstaller.WriteConfigurationFile();
            progress.Report(5, 9, "Registering URL ACLs...");
            instanceInstaller.RegisterUrlAcl();
            progress.Report(6, 9, "Creating queues...");
            instanceInstaller.SetupInstance();

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
            instanceInstaller.ReportCard.SetStatus();
            return instanceInstaller.ReportCard;
        }

        internal ReportCard Upgrade(string instanceName, IProgress<ProgressDetails> progress = null)
        {
            progress = progress ?? new Progress<ProgressDetails>();

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

            progress.Report(1, 5, "Backing up app.config...");
            var backupFile = instance.BackupAppConfig();
            try
            {
                progress.Report(2, 5, "Upgrading Files...");
                instance.UpgradeFiles(ZipInfo.FilePath);
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

        internal ReportCard Update(MonitoringInstance instance, bool startService)
        {
            try
            {
                instance.ReportCard = new ReportCard();
                instance.ValidateChanges();
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
            progress = progress ?? new Progress<ProgressDetails>();
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

        internal CheckLicenseResult CheckLicenseIsValid()
        {
            DateTime releaseDate;
            var license = LicenseManager.FindLicense();
            if (license.Details.HasLicenseExpired())
            {
                return new CheckLicenseResult(false, "License has expired");
            }

            if (!license.Details.ValidForServiceControl)
            {
                return new CheckLicenseResult(false, "This license edition does not include ServiceControl");
            }

            if (ZipInfo.TryReadMonitoringReleaseDate(out releaseDate))
            {
                if (license.Details.ReleaseNotCoveredByMaintenance(releaseDate))
                {
                    return new CheckLicenseResult(false, "License does not cover this release of ServiceControl Monitoring. Upgrade protection expired");
                }
            }
            else
            {
                throw new Exception("Failed to retrieve release date for new version");
            }
            return new CheckLicenseResult(true);
        }

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