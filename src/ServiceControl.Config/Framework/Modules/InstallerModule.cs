namespace ServiceControl.Config.Framework.Modules
{
    using System;
    using System.IO;
    using System.Reflection;
    using Autofac;
    using ServiceControlInstaller.Engine.FileSystem;
    using ServiceControlInstaller.Engine.Instances;
    using ServiceControlInstaller.Engine.ReportCard;
    using ServiceControlInstaller.Engine.Validation;
    using Module = Autofac.Module;

    public class InstallerModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<Installer>().SingleInstance();
        }
    }

    public class Installer
    {
        public ServiceControlZipInfo ZipInfo { get; }

        public Installer()
        {
            var appDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            ZipInfo = ServiceControlZipInfo.Find(appDirectory);
        }

        internal ReportCard Add(ServiceControlInstanceMetadata details, IProgress<ProgressDetails> progress, Func<PathInfo, bool> promptToProceed)
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
            progress.Report(6, 9, "Setup instance... (this can take some time)");
            instanceInstaller.SetupInstance();

            if (!instanceInstaller.ReportCard.HasErrors)
            {
                progress.Report(7, 9, "Registering service...");
                instanceInstaller.RegisterService();
                //Post Installation
                progress.Report(8, 9, "Starting service...");
                var instance = ServiceControlInstance.FindByName(instanceInstaller.Name);
                if (!instance.TryStartService())
                {
                    instanceInstaller.ReportCard.Warnings.Add($"New instance did not startup - please check configuration for {instance.Name}");
                }
            }
            instanceInstaller.ReportCard.SetStatus();
            return instanceInstaller.ReportCard;
        }

        internal ReportCard Upgrade(string instanceName, InstanceUpgradeOptions upgradeOptions, IProgress<ProgressDetails> progress = null)
        {
            progress = progress ?? new Progress<ProgressDetails>();

            var instance = ServiceControlInstance.FindByName(instanceName);
            instance.ReportCard = new ReportCard();
            ZipInfo.ValidateZip();

            progress.Report(0, 6, "Stopping instance...");
            if (!instance.TryStopService())
            {
                return new ReportCard
                {
                    Errors = { "Service failed to stop" },
                    Status = Status.Failed
                };
            }

            progress.Report(1, 6, "Backing up app.config...");
            var backupFile = instance.BackupAppConfig();
            try
            {
                progress.Report(2, 6, "Upgrading Files...");
                instance.UpgradeFiles(ZipInfo.FilePath);

                progress.Report(3, 6, "Moving database...");
                instance.MoveRavenDatabase(instance.DBPath);
            }
            finally
            {
                progress.Report(4, 6, "Restoring app.config...");
                instance.RestoreAppConfig(backupFile);
            }

            upgradeOptions.ApplyChangesToInstance(instance);

            progress.Report(5, 6, "Upgrading instance... (this can take some time)");
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
            progress.Report(0, 9, "Stopping instance...");
            var instance = ServiceControlInstance.FindByName(instanceName);
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

            progress.Report(1, 9, "Disabling startup...");
            instance.Service.SetStartupMode("Disabled");

            progress.Report(2, 9, "Deleting service...");
            instance.Service.Delete();

            progress.Report(3, 9, "Removing URL ACL...");
            instance.RemoveUrlAcl();

            progress.Report(4, 9, "Deleting install...");
            instance.RemoveBinFolder();

            if (removeLogs)
            {
                progress.Report(5, 9, "Deleting logs...");
                instance.RemoveLogsFolder();
            }
            if (removeDB)
            {
                progress.Report(6, 9, "Deleting database...");
                instance.RemoveDataBaseFolder();
                progress.Report(7, 9, "Deleting body storage...");
                instance.RemoveBodyStorageFolder();
                progress.Report(8, 9, "Deleting injestion cache...");
                instance.RemoveInjestionFolder();
            }

            progress.Report(new ProgressDetails());

            instance.ReportCard.SetStatus();
            return instance.ReportCard;
        }


    }
}