namespace ServiceControl.Config.UI.InstanceDetails
{
    using System;
    using System.Linq;
    using System.ServiceProcess;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows.Input;
    using Caliburn.Micro;
    using Commands;
    using Events;
    using Extensions;
    using Framework;
    using Framework.Rx;
    using NuGet.Versioning;
    using ServiceControlInstaller.Engine;
    using ServiceControlInstaller.Engine.Instances;

    class InstanceDetailsViewModel : RxProgressScreen, IHandle<PostRefreshInstances>
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
            AdvancedServiceControlOptionsCommand advancedOptionsServiceControlCommand)
        {
            OpenUrl = new OpenURLCommand();
            CopyToClipboard = new CopyToClipboardCommand();
            StartCommand = Command.Create(() => StartService());
            StopCommand = Command.Create(() => StopService());

            if (instance.GetType() == typeof(ServiceControlInstance))
            {
                ServiceControlInstance = (ServiceControlInstance)instance;
                NewVersion = Constants.CurrentVersion;
                EditCommand = showServiceControlEditScreenCommand;
                UpgradeToNewVersionCommand = upgradeServiceControlCommand;
                AdvancedOptionsCommand = advancedOptionsServiceControlCommand;
                InstanceType = InstanceType.ServiceControl;
                return;
            }

            if (instance.GetType() == typeof(MonitoringInstance))
            {
                MonitoringInstance = (MonitoringInstance)instance;
                NewVersion = Constants.CurrentVersion;
                EditCommand = showEditMonitoringScreenCommand;
                UpgradeToNewVersionCommand = upgradeMonitoringCommand;
                AdvancedOptionsCommand = advancedOptionsMonitoringCommand;
                InstanceType = InstanceType.Monitoring;
                return;
            }

            if (instance.GetType() == typeof(ServiceControlAuditInstance))
            {
                ServiceControlAuditInstance = (ServiceControlAuditInstance)instance;
                NewVersion = Constants.CurrentVersion;
                EditCommand = showAuditEditScreenCommand;
                UpgradeToNewVersionCommand = upgradeAuditCommand;
                AdvancedOptionsCommand = advancedOptionsServiceControlCommand;
                InstanceType = InstanceType.ServiceControlAudit;
                return;
            }

            throw new Exception("Unknown instance type");
        }

        public void UpdateServiceInstance(BaseService updatedInstance)
        {
            if (updatedInstance.Name != ServiceInstance.Name)
            {
                throw new ArgumentException("Cannot update with an instance of a different name");
            }

            if (updatedInstance.GetType() != ServiceInstance.GetType())
            {
                throw new ArgumentException("Cannot update with an instance of a different type");
            }

            // Update the internal reference based on type
            if (updatedInstance.GetType() == typeof(ServiceControlInstance))
            {
                ServiceControlInstance = (ServiceControlInstance)updatedInstance;
            }
            else if (updatedInstance.GetType() == typeof(MonitoringInstance))
            {
                MonitoringInstance = (MonitoringInstance)updatedInstance;
            }
            else if (updatedInstance.GetType() == typeof(ServiceControlAuditInstance))
            {
                ServiceControlAuditInstance = (ServiceControlAuditInstance)updatedInstance;
            }
        }

        public BaseService ServiceInstance =>
            (BaseService)ServiceControlInstance ??
            (BaseService)MonitoringInstance ??
            ServiceControlAuditInstance;

        public bool InMaintenanceMode =>
            ServiceControlInstance?.InMaintenanceMode == true ||
            ServiceControlAuditInstance?.InMaintenanceMode == true;

        public bool IsServiceControlInstance => ServiceInstance?.GetType() == typeof(ServiceControlInstance);
        public bool IsMonitoringInstance => ServiceInstance?.GetType() == typeof(MonitoringInstance);
        public bool IsServiceControlAudit => ServiceInstance?.GetType() == typeof(ServiceControlAuditInstance);

        public string Name => ServiceInstance.Name;

        public string InstanceName => ServiceInstance.InstanceName;

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

        public string UrlHeading
        {
            get
            {
                if (IsServiceControlInstance)
                {
                    if (ServiceControlInstance.EnableIntegratedServicePulse)
                    {
                        return "SERVICEPULSE";
                    }
                }

                return "URL";
            }
        }

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

        public SemanticVersion Version => ServiceInstance.Version;

        public InstanceType InstanceType { get; set; }

        public string InstanceTypeDisplayName => InstanceType.GetDescription();

        public string InstanceTypeIcon => InstanceType == InstanceType.Monitoring ? "MonitoringInstanceIcon" : "ServiceControlInstanceIcon";

        public SemanticVersion NewVersion { get; }

        public bool HasNewVersion => Version < NewVersion;

        public TransportInfo Transport => HasConfigurationError ? null : ((ITransportConfig)ServiceInstance).TransportPackage;

        public string Persister
        {
            get
            {
                if (HasConfigurationError)
                {
                    return string.Empty; // Leave blank for corrupt instances
                }

                if (ServiceInstance is IServiceControlInstance primaryInstance)
                {
                    return primaryInstance.PersistenceManifest?.DisplayName ?? "Unknown";
                }

                if (ServiceInstance is IServiceControlAuditInstance auditInstance)
                {
                    return auditInstance.PersistenceManifest?.DisplayName ?? "Unknown";
                }

                if (ServiceInstance is IMonitoringInstance)
                {
                    return "InMemory";
                }

                return "Unknown";
            }
        }

        public string Status
        {
            get
            {
                // If there's a configuration error, show that instead of service status
                if (HasConfigurationError)
                {
                    return "ERROR";
                }

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
                // If there's a configuration error, don't show running icon
                if (HasConfigurationError)
                {
                    return false;
                }

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
                // If there's a configuration error, don't show stopped icon either
                if (HasConfigurationError)
                {
                    return false;
                }

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
                // Don't allow start for instances with configuration errors
                if (HasConfigurationError)
                {
                    return false;
                }

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
                // Don't allow stop for instances with configuration errors
                if (HasConfigurationError)
                {
                    return false;
                }

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

        public bool HasConfigurationError => !string.IsNullOrEmpty(ServiceInstance?.ConfigurationLoadError);

        public bool AllowEdit => !HasConfigurationError; // Disable edit for corrupt instances

        public string ConfigurationErrorMessage => ServiceInstance?.ConfigurationLoadError;

        public ICommand OpenUrl { get; private set; }

        public ICommand CopyToClipboard { get; private set; }

        public ICommand EditCommand { get; private set; }

        public ICommand AdvancedOptionsCommand { get; private set; }

        public ICommand StartCommand { get; private set; }

        public ICommand StopCommand { get; private set; }

        public ICommand UpgradeToNewVersionCommand { get; private set; }

        public bool Exists() => ServiceInstance.Service.Exists();

        public Task HandleAsync(PostRefreshInstances message, CancellationToken cancellationToken = default)
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
            NotifyOfPropertyChange("UrlHeading");
            NotifyOfPropertyChange(nameof(HasConfigurationError));
            NotifyOfPropertyChange(nameof(AllowEdit));
            NotifyOfPropertyChange(nameof(ConfigurationErrorMessage));
            return Task.CompletedTask;
        }

        public async Task<bool> StartService(IProgressObject progress = null, CancellationToken cancellationToken = default)
        {
            var disposeProgress = progress == null;
            var result = false;

            try
            {
                progress ??= this.GetProgressObject(string.Empty);

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

        public async Task<bool> StopService(IProgressObject progress = null, CancellationToken cancellationToken = default)
        {
            var disposeProgress = progress == null;
            var result = false;

            try
            {
                progress ??= this.GetProgressObject(string.Empty);

                progress.Report(new ProgressDetails("Stopping Service"));
                await Task.Run(() =>
                {
                    result = ServiceInstance.TryStopService();
                    if (InstanceType == InstanceType.ServiceControl && InMaintenanceMode)
                    {
                        ServiceControlInstance.DisableMaintenanceMode();
                    }
                }, cancellationToken);

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
            try
            {
                ServiceInstance.Reload();
            }
            catch (Exception ex)
            {
                // Handle reload failure gracefully - configuration error will be shown in UI
                ServiceInstance.ConfigurationLoadError = $"Failed to load configuration file '{ServiceInstance.ConfigurationFilePath}': {ex.Message}";
                // Ensure basic properties are set so UI can still display the instance
                if (string.IsNullOrEmpty(ServiceInstance.InstanceName))
                {
                    ServiceInstance.InstanceName = ServiceInstance.Name;
                }
            }

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
