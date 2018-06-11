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
    using ServiceControlInstaller.Engine;
    using ServiceControlInstaller.Engine.Instances;

    class InstanceDetailsViewModel : RxProgressScreen, IHandle<RefreshInstances>
    {
        public InstanceDetailsViewModel(
            BaseService instance,
            EditServiceControlInstanceCommand showEditServiceControlScreenCommand,
            EditMonitoringInstanceCommand showEditMonitoringScreenCommand,
            UpgradeServiceControlInstanceCommand upgradeServiceControlCommand,
            UpgradeMonitoringInstanceCommand upgradeMonitoringCommand,
            AdvancedMonitoringOptionsCommand advancedOptionsMonitoringCommand,
            AdvancedServiceControlOptionsCommand advancedOptionsServiceControlCommand,

            ServiceControlInstanceInstaller serviceControlinstaller,
            MonitoringInstanceInstaller monitoringinstaller)
        {
            OpenUrl = new OpenURLCommand();
            CopyToClipboard = new CopyToClipboardCommand();
            StartCommand = Command.Create(() => StartService());
            StopCommand = Command.Create(() => StopService());
            
            ServiceInstance = instance;

            if (instance.GetType() == typeof(ServiceControlInstance))
            {
                ServiceControlInstance = (ServiceControlInstance)instance;
                NewVersion = serviceControlinstaller.ZipInfo.Version;
                EditCommand = showEditServiceControlScreenCommand;
                UpgradeToNewVersionCommand = upgradeServiceControlCommand;
                AdvancedOptionsCommand = advancedOptionsServiceControlCommand;
                InstanceType = InstanceType.ServiceControl;
                return;
            }
            if (instance.GetType() == typeof(MonitoringInstance))
            {
                MonitoringInstance = (MonitoringInstance) instance;
                NewVersion = monitoringinstaller.ZipInfo.Version;
                EditCommand = showEditMonitoringScreenCommand;
                UpgradeToNewVersionCommand = upgradeMonitoringCommand;
                AdvancedOptionsCommand = advancedOptionsMonitoringCommand;
                InstanceType = InstanceType.Monitoring;
                return;
            }
            throw new Exception("Unknown instance type");
        }

        public BaseService ServiceInstance { get; }

        public ServiceControlInstance ServiceControlInstance;
        public MonitoringInstance MonitoringInstance;

        public bool InMaintenanceMode => ServiceControlInstance?.InMaintenanceMode ?? false;

        public bool IsServiceControlInstance => ServiceInstance?.GetType() == typeof(ServiceControlInstance);
        public bool IsMonitoringInstance => ServiceInstance?.GetType() == typeof(MonitoringInstance);

        public string Name => ServiceInstance.Name;

        public string Host => ((IURLInfo)ServiceInstance).Url;

        public string BrowsableUrl => ((IURLInfo)ServiceInstance).BrowsableUrl;
        
        public string InstallPath => ((IServicePaths)ServiceInstance).InstallPath;

        public string DBPath => ServiceControlInstance?.DBPath;

        public string LogPath => ((IServicePaths)ServiceInstance).LogPath;

        public Version Version => ServiceInstance.Version;

        public InstanceType InstanceType { get; set; }

        public Version NewVersion { get; }

        public bool HasNewVersion => Version < NewVersion;

        public string Transport => ((ITransportConfig) ServiceInstance).TransportPackage;

        public bool IsUpdatingDataStore => ServiceControlInstance?.IsUpdatingDataStore ?? false;

        public string Status
        {
            get
            {
                try
                {
                    return ServiceInstance.Service.Status.ToString().ToUpperInvariant();
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
                    return ServiceInstance.Service.Status != ServiceControllerStatus.Stopped;
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
                    return ServiceInstance.Service.Status == ServiceControllerStatus.Stopped;
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
                    return !dontAllowStartOn.Any(p => p.Equals(ServiceInstance.Service.Status));
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
                    return !dontAllowStopOn.Any(p => p.Equals(ServiceInstance.Service.Status));
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

        public ICommand AdvancedOptionsCommand { get; private set; }

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

        public async Task<bool> StartService(IProgressObject progress = null)
        {
            var disposeProgress = progress == null;
            var result = false;

            try
            {
                progress = progress ?? this.GetProgressObject(String.Empty);
                
                // We need this one here in case the user stopped the service by other means
                if (InstanceType == InstanceType.ServiceControl && InMaintenanceMode)
                {
                    ServiceControlInstance.DisableMaintenanceMode();
                }
                progress.Report(new ProgressDetails("Starting Service"));
                await Task.Run(() => result = ServiceInstance.TryStartService());

                UpdateServiceProperties();

                return result;
            }
            finally
            {
                if (disposeProgress)
                {
                    progress?.Dispose();
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
                    result = ServiceInstance.TryStopService();
                    if (InstanceType == InstanceType.ServiceControl && InMaintenanceMode)
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
                    progress?.Dispose();
                }
            }
        }

        void UpdateServiceProperties()
        {
            ServiceInstance.RefreshServiceProperties();

            NotifyOfPropertyChange("Status");
            NotifyOfPropertyChange("AllowStop");
            NotifyOfPropertyChange("AllowStart");
            NotifyOfPropertyChange("IsRunning");
            NotifyOfPropertyChange("IsStopped");
            NotifyOfPropertyChange("InMaintenanceMode");
            NotifyOfPropertyChange("IsUpdatingDataStore");
        }
    }
}