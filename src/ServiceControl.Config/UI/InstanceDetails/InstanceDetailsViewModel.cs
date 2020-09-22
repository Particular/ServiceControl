namespace ServiceControl.Config.UI.InstanceDetails
{
    using System;
    using System.Linq;
    using System.ServiceProcess;
    using System.Threading.Tasks;
    using System.Windows.Input;
    using Caliburn.Micro;
    using Commands;
    using Events;
    using Extensions;
    using Framework;
    using Framework.Modules;
    using Framework.Rx;
    using ServiceControlInstaller.Engine;
    using ServiceControlInstaller.Engine.Instances;

    class InstanceDetailsViewModel : RxProgressScreen, IHandle<RefreshInstances>
    {
        public InstanceDetailsViewModel(
            BaseService instance,
            EditServiceControlAuditInstanceCommand showAuditEditScreenCommand,
            EditServiceControlInstanceCommand showServiceControlEditScreenCommand,
            EditMonitoringInstanceCommand showEditMonitoringScreenCommand,
            UpgradeServiceControlInstanceCommand upgradeServiceControlCommand,
            UpgradeMonitoringInstanceCommand upgradeMonitoringCommand,
            UpgradeAuditInstanceCommand upgradeAuditCommand,
            AdvancedMonitoringOptionsCommand advancedOptionsMonitoringCommand,
            AdvancedServiceControlOptionsCommand advancedOptionsServiceControlCommand,
            ServiceControlInstanceInstaller serviceControlinstaller,
            ServiceControlAuditInstanceInstaller serviceControlAuditInstaller,
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
                EditCommand = showServiceControlEditScreenCommand;
                UpgradeToNewVersionCommand = upgradeServiceControlCommand;
                AdvancedOptionsCommand = advancedOptionsServiceControlCommand;
                InstanceType = InstanceType.ServiceControl;
                return;
            }

            if (instance.GetType() == typeof(MonitoringInstance))
            {
                MonitoringInstance = (MonitoringInstance)instance;
                NewVersion = monitoringinstaller.ZipInfo.Version;
                EditCommand = showEditMonitoringScreenCommand;
                UpgradeToNewVersionCommand = upgradeMonitoringCommand;
                AdvancedOptionsCommand = advancedOptionsMonitoringCommand;
                InstanceType = InstanceType.Monitoring;
                return;
            }

            if (instance.GetType() == typeof(ServiceControlAuditInstance))
            {
                ServiceControlAuditInstance = (ServiceControlAuditInstance)instance;
                NewVersion = serviceControlAuditInstaller.ZipInfo.Version;
                EditCommand = showAuditEditScreenCommand;
                UpgradeToNewVersionCommand = upgradeAuditCommand;
                AdvancedOptionsCommand = advancedOptionsServiceControlCommand;
                InstanceType = InstanceType.ServiceControlAudit;
                return;
            }

            throw new Exception("Unknown instance type");
        }

        public BaseService ServiceInstance { get; }

        public bool InMaintenanceMode =>
            ServiceControlInstance?.InMaintenanceMode == true || 
            ServiceControlAuditInstance?.InMaintenanceMode == true;

        public bool IsServiceControlInstance => ServiceInstance?.GetType() == typeof(ServiceControlInstance);
        public bool IsMonitoringInstance => ServiceInstance?.GetType() == typeof(MonitoringInstance);
        public bool IsServiceControlAudit => ServiceInstance?.GetType() == typeof(ServiceControlAuditInstance);

        public string Name => ServiceInstance.Name;

        public string Host
        {
            get
            {
                if (ServiceInstance is IURLInfo info)
                {
                    return info?.Url;
                }

                return null;
            }
        }

        public string BrowsableUrl
        {
            get
            {
                if (ServiceInstance is IURLInfo info)
                {
                    return info?.BrowsableUrl;
                }

                return null;
            }
        }

        public bool HasBrowsableUrl => ServiceInstance is IURLInfo;

        public string InstallPath => ((IServicePaths)ServiceInstance).InstallPath;

        public string DBPath => GetDBPathIfAvailable();

        string GetDBPathIfAvailable()
        {
            if (IsServiceControlInstance)
            {
                return ServiceControlInstance?.DBPath;
            }

            if (IsServiceControlAudit)
            {
                return ServiceControlAuditInstance?.DBPath;
            }

            return null;
        }

        public bool HasBrowsableDBPath => !string.IsNullOrEmpty(DBPath);

        public string LogPath => ((IServicePaths)ServiceInstance).LogPath;

        public Version Version => ServiceInstance.Version;

        public InstanceType InstanceType { get; set; }

        public string InstanceTypeDisplayName => InstanceType.GetDescription();

        public string InstanceTypeIcon
        {
            get { return InstanceType == InstanceType.Monitoring ? "MonitoringInstanceIcon" : "ServiceControlInstanceIcon"; }
        }

        public Version NewVersion { get; }

        public bool HasNewVersion => Version < NewVersion;

        public TransportInfo Transport => ((ITransportConfig)ServiceInstance).TransportPackage;

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
                        ServiceControllerStatus.StopPending
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
                        ServiceControllerStatus.StopPending
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
            ServiceInstance.Reload();

            NotifyOfPropertyChange("Status");
            NotifyOfPropertyChange("AllowStop");
            NotifyOfPropertyChange("AllowStart");
            NotifyOfPropertyChange("IsRunning");
            NotifyOfPropertyChange("IsStopped");
            NotifyOfPropertyChange("InMaintenanceMode");
        }

        public ServiceControlInstance ServiceControlInstance;
        public ServiceControlAuditInstance ServiceControlAuditInstance;
        public MonitoringInstance MonitoringInstance;
    }
}