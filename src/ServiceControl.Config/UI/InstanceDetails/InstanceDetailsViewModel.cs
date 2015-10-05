namespace ServiceControl.Config.UI.InstanceDetails
{
    using System;
    using System.Linq;
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

        public string Name
        {
            get { return ServiceControlInstance.Name; }
        }

        public string Host
        {
            get { return ServiceControlInstance.Url; }
        }

        public string BrowsableUrl
        {
            get
            {
                // When hostname is a wildcard this returns a URL based on localhost or machinename
                return ServiceControlInstance.BrowsableUrl;
            }
        }

        public string InstallPath
        {
            get { return ServiceControlInstance.InstallPath; }
        }

        public string DBPath
        {
            get { return ServiceControlInstance.DBPath; }
        }

        public string LogPath
        {
            get { return ServiceControlInstance.LogPath; }
        }

        public Version Version
        {
            get { return ServiceControlInstance.Version; }
        }

        public Version NewVersion { get; private set; }

        public bool HasNewVersion
        {
            get { return Version < NewVersion; }
        }

        public string Transport
        {
            get { return ServiceControlInstance.TransportPackage; }
        }

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
                    return ServiceControlInstance.Service.Status != ServiceControllerStatus.Stopped;
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

        public bool AllowStart
        {
            get
            {
                try
                {
                    var dontAllowStartOn = new[]
                    {
                        ServiceControllerStatus.Running,
                        ServiceControllerStatus.StartPending,
                        ServiceControllerStatus.StopPending,
                    };
                    return !dontAllowStartOn.Any(p => p.Equals(ServiceControlInstance.Service.Status));
                }
                catch (InvalidOperationException)
                {
                    return false;
                }
            }
        }

        public bool AllowStop
        {
            get
            {
                try
                {
                    var dontAllowStopOn = new[]
                    {
                        ServiceControllerStatus.Stopped,
                        ServiceControllerStatus.StartPending,
                        ServiceControllerStatus.StopPending,
                    };
                    return !dontAllowStopOn.Any(p => p.Equals(ServiceControlInstance.Service.Status));
                }
                catch (InvalidOperationException)
                {
                    return false;
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
            NotifyOfPropertyChange("HasNewVersion");
            NotifyOfPropertyChange("Transport");
            NotifyOfPropertyChange("BrowsableUrl");
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
            NotifyOfPropertyChange("AllowStop");
            NotifyOfPropertyChange("AllowStart");
            NotifyOfPropertyChange("IsRunning");
            NotifyOfPropertyChange("IsStopped");
        }
    }
}