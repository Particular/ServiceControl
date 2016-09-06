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
    using ServiceControlInstaller.Engine.Configuration;
    using ServiceControlInstaller.Engine.Instances;

    class InstanceDetailsViewModel : RxProgressScreen, IHandle<RefreshInstances>
    {
        public InstanceDetailsViewModel(
            ServiceControlInstance serviceControlInstance,
            EditInstanceCommand showEditInstanceScreenCommand,
            UpgradeInstanceCommand upgradeInstanceToNewVersionCommand,
            AdvanceOptionsCommand advanceOptionsCommand,
            Installer installer)
        {
            ServiceControlInstance = serviceControlInstance;

            NewVersion = installer.ZipInfo.Version;

            OpenUrl = new OpenURLCommand();
            CopyToClipboard = new CopyToClipboardCommand();

            EditCommand = showEditInstanceScreenCommand;
            UpgradeToNewVersionCommand = upgradeInstanceToNewVersionCommand;

            StartCommand = Command.Create(() => StartService());
            StopCommand = Command.Create(() => StopService());

            AdvanceOptionsCommand = advanceOptionsCommand;

            BodyStoragePathVisible = serviceControlInstance.Version >= SettingsList.BodyStoragePath.SupportedFrom;
            InjestionCachePathVisible = serviceControlInstance.Version >= SettingsList.InjestionCachePath.SupportedFrom;
        }

        public bool InjestionCachePathVisible { get; set; }

        public bool BodyStoragePathVisible { get; set; }

        public ServiceControlInstance ServiceControlInstance { get; }

        public bool InMaintenanceMode => ServiceControlInstance.InMaintenanceMode;

        public string Name => ServiceControlInstance.Name;

        public string Host => ServiceControlInstance.Url;

        public string BrowsableUrl => ServiceControlInstance.BrowsableUrl;

        public string StorageUrl => ServiceControlInstance.StorageUrl;

        public string InstallPath => ServiceControlInstance.InstallPath;

        public string DBPath => ServiceControlInstance.DBPath;

        public string BodyStoragePath => ServiceControlInstance.BodyStoragePath;

        public string InjestionCachePath => ServiceControlInstance.InjestionCachePath;

        public string LogPath => ServiceControlInstance.LogPath;

        public Version Version => ServiceControlInstance.Version;

        public Version NewVersion { get; }

        public bool HasNewVersion => Version < NewVersion;

        public string Transport => ServiceControlInstance.TransportPackage;

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

        public ICommand CopyToClipboard { get; private set; }

        public ICommand EditCommand { get; private set; }

        public ICommand AdvanceOptionsCommand { get; private set; }

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
            NotifyOfPropertyChange("InMaintenanceMode");
        }

        public async Task<bool> StartService(IProgressObject progress = null)
        {
            var disposeProgress = progress == null;
            var result = false;

            try
            {
                progress = progress ?? this.GetProgressObject(String.Empty);
                
                // We need this one here in case the user stopped the service by other means
                if (InMaintenanceMode)
                {
                    ServiceControlInstance.DisableMaintenanceMode();
                }
                progress.Report(new ProgressDetails("Starting Service"));
                await Task.Run(() => result = ServiceControlInstance.TryStartService());

                UpdateServiceProperties();

                return result;
            }
            finally
            {
                if (disposeProgress)
                {
                    progress.Dispose();
                }
            }
        }

        public async Task<bool> StopService(IProgressObject progress = null)
        {
            var disposeProgress = progress == null;
            var result = false;

            try
            {
                progress = progress ?? this.GetProgressObject(String.Empty);

                progress.Report(new ProgressDetails("Stopping Service"));
                await Task.Run(() =>
                {
                    result = ServiceControlInstance.TryStopService();
                    if (InMaintenanceMode)
                    {
                        ServiceControlInstance.DisableMaintenanceMode();
                    }
                });

                UpdateServiceProperties();

                return result;
            }
            finally
            {
                if (disposeProgress)
                {
                    progress.Dispose();
                }
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
            NotifyOfPropertyChange("InMaintenanceMode");
        }
    }
}