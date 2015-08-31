namespace ServiceControl.Config.UI.InstanceDetails
{
    using System;
    using System.ServiceProcess;
    using System.Threading.Tasks;
    using System.Windows.Input;
    using Caliburn.Micro;
    using Events;
    using ServiceControl.Config.Commands;
    using ServiceControl.Config.Framework;
    using ServiceControl.Config.Framework.Modules;
    using ServiceControl.Config.Framework.Rx;
    using ServiceControlInstaller.Engine.Instances;

    class InstanceDetailsViewModel : RxProgressScreen, IHandle<RefreshInstances>
    {
        public InstanceDetailsViewModel(
            ServiceControlInstance serviceControlInstance,
            EditInstanceCommand showEditInstanceScreenCommand,
            DeleteInstanceCommand deleteInstanceCommand,
            UpgradeInstanceCommand upgradeInstanceToNewVersionCommand,
            Installer installer)
        {
            ServiceControlInstance = serviceControlInstance;

            NewVersion = installer.ZipInfo.Version;

            OpenUrl = new OpenURLCommand();

            EditCommand = showEditInstanceScreenCommand;
            DeleteCommand = deleteInstanceCommand;
            UpgradeToNewVersionCommand = upgradeInstanceToNewVersionCommand;

            StartCommand = Command.Create(() => StartService());
            StopCommand = Command.Create(() => StopService());
        }

        public ServiceControlInstance ServiceControlInstance { get; private set; }

        public string Name { get { return ServiceControlInstance.Name; } }

        public string Host { get { return ServiceControlInstance.Url; } }

        public string InstallPath { get { return ServiceControlInstance.InstallPath; } }

        public string DBPath { get { return ServiceControlInstance.DBPath; } }

        public string LogPath { get { return ServiceControlInstance.LogPath; } }

        public Version Version { get { return ServiceControlInstance.Version; } }

        public Version NewVersion { get; private set; }

        public bool HasNewVersion { get { return Version < NewVersion; } }

        public string Transport { get { return ServiceControlInstance.TransportPackage; } }

        public string Status
        {
            get
            {
                try
                {
                    return ServiceControlInstance.Service.Status.ToString().ToUpperInvariant();
                }
                catch (InvalidOperationException)
                {
                    return string.Empty;
                }
            }
        }

        public bool IsRunning
        {
            get
            {
                try
                {
                    return ServiceControlInstance.Service.Status == ServiceControllerStatus.Running;
                }
                catch (InvalidOperationException)
                {
                    return false;
                }
            }
        }

        public bool IsStopped
        {
            get
            {
                try
                {
                    return ServiceControlInstance.Service.Status == ServiceControllerStatus.Stopped;
                }
                catch (InvalidOperationException)
                {
                    return true;
                }
            }
        }

        public ICommand OpenUrl { get; private set; }

        public ICommand EditCommand { get; private set; }

        public ICommand DeleteCommand { get; private set; }

        public ICommand StartCommand { get; private set; }

        public ICommand StopCommand { get; private set; }

        public ICommand UpgradeToNewVersionCommand { get; private set; }

        public void Handle(RefreshInstances message)
        {
            UpdateServiceProperties();
            NotifyOfPropertyChange("Name");
            NotifyOfPropertyChange("Host");
            NotifyOfPropertyChange("InstallPath");
            NotifyOfPropertyChange("DBPath");
            NotifyOfPropertyChange("LogPath");
            NotifyOfPropertyChange("Version");
            NotifyOfPropertyChange("NewVersion");
            NotifyOfPropertyChange("Transport");
        }

        public async Task StartService()
        {
            using (var progress = this.GetProgressObject(""))
            {
                progress.Report(new ProgressDetails("Starting Service"));
                await Task.Run(() => ServiceControlInstance.TryStartService());
                UpdateServiceProperties();
            }
        }

        public async Task StopService()
        {
            using (var progress = this.GetProgressObject(""))
            {
                progress.Report(new ProgressDetails("Stopping Service"));
                await Task.Run(() => ServiceControlInstance.TryStopService());
                UpdateServiceProperties();
            }
        }

        void UpdateServiceProperties()
        {
            ServiceControlInstance.Service.Refresh();
            NotifyOfPropertyChange("Status");
            NotifyOfPropertyChange("IsRunning");
            NotifyOfPropertyChange("IsStopped");
        }
    }
}