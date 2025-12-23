namespace ServiceControl.Config.UI.AdvancedOptions
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
    using Framework;
    using Framework.Rx;
    using NuGet.Versioning;
    using ReactiveUI;
    using ServiceControlInstaller.Engine.Configuration.ServiceControl;
    using ServiceControlInstaller.Engine.Instances;

    class ServiceControlAdvancedViewModel : RxProgressScreen, IHandle<PostRefreshInstances>
    {
        public ServiceControlAdvancedViewModel(
            BaseService instance,
            IEventAggregator eventAggregator,
            StartServiceControlInMaintenanceModeCommand maintenanceModeCommand,
            DeleteServiceControlInstanceCommand deleteInstanceCommand,
            ForceUpgradePrimaryInstanceCommand forceUpgradePrimaryCommand,
            ForceUpgradeAuditInstanceCommand forceUpgradeAuditCommand
            )
        {
            ServiceControlInstance = (ServiceControlBaseService)instance;
            DisplayName = "ADVANCED OPTIONS";

            StartServiceInMaintenanceModeCommand = ReactiveCommand.CreateFromTask<ServiceControlAdvancedViewModel>(async _ =>
            {
                await maintenanceModeCommand.ExecuteAsync(this);
                await eventAggregator.PublishOnUIThreadAsync(new RefreshInstances());
            });
            DeleteCommand = deleteInstanceCommand;

            if (instance is ServiceControlAuditInstance)
            {
                ForceUpgradeCommand = forceUpgradeAuditCommand;
            }
            else if (instance is ServiceControlInstance)
            {
                ForceUpgradeCommand = forceUpgradePrimaryCommand;
            }

            OpenUrl = new OpenURLCommand();
            CopyToClipboard = new CopyToClipboardCommand();
            StopMaintenanceModeCommand = ReactiveCommand.CreateFromTask<ServiceControlAdvancedViewModel>(async _ =>
            {
                await StopService();
                await eventAggregator.PublishOnUIThreadAsync(new RefreshInstances());
            });
            Cancel = Command.Create(async () =>
            {
                await TryCloseAsync(false);
                await eventAggregator.PublishOnUIThreadAsync(new RefreshInstances());
            }, () => !InProgress);
        }

        public ServiceControlBaseService ServiceControlInstance { get; }

        public ICommand StartServiceInMaintenanceModeCommand { get; set; }
        public ICommand StopMaintenanceModeCommand { get; set; }

        public bool MaintenanceModeSupported => ServiceControlInstance.Version >= ServiceControlSettings.MaintenanceMode.SupportedFrom;

        public ICommand DeleteCommand { get; set; }

        public ICommand ForceUpgradeCommand { get; set; }

        public ICommand Cancel { get; set; }

        public ICommand OpenUrl { get; }

        public ICommand CopyToClipboard { get; }

        public string Name => ServiceControlInstance.Name;

        public bool InMaintenanceMode => ServiceControlInstance.InMaintenanceMode;

        public string RavenDbStudioUrl => ServiceControlInstance.RavenDbStudioUrl;

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
                    return !dontAllowStopOn.Any(p => p.Equals(ServiceControlInstance.Service.Status));
                }
                catch (InvalidOperationException)
                {
                    return false;
                }
            }
        }

        static readonly SemanticVersion ForcedUpgradeMaxVersion = new(5, 0, 0);

        public bool ForcedUpgradeAllowed => VersionComparer.Version.Compare(ServiceControlInstance.Version, ForcedUpgradeMaxVersion) < 0 && ForceUpgradeCommand.CanExecute(this);

        public string ForcedUpgradeBackupLocation => ServiceControlInstance.DatabaseBackupPath;

        public Task HandleAsync(PostRefreshInstances message, CancellationToken cancellationToken)
        {
            NotifyOfPropertyChange("AllowStop");
            NotifyOfPropertyChange("IsRunning");
            NotifyOfPropertyChange("IsStopped");
            NotifyOfPropertyChange("InMaintenanceMode");
            return Task.CompletedTask;
        }

        public async Task<bool> StartService(IProgressObject progress, bool maintenanceMode)
        {
            var disposeProgress = progress == null;
            var result = false;
            try
            {
                progress ??= this.GetProgressObject();

                progress.Report(new ProgressDetails("Starting Service"));
                await Task.Run(() =>
                {
                    if (maintenanceMode)
                    {
                        ServiceControlInstance.EnableMaintenanceMode();
                    }
                    else
                    {
                        ServiceControlInstance.DisableMaintenanceMode();
                    }

                    result = ServiceControlInstance.TryStartService();
                });

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
                progress ??= this.GetProgressObject();

                progress.Report(new ProgressDetails("Stopping Service"));
                await Task.Run(() =>
                {
                    result = ServiceControlInstance.TryStopService();
                    if (InMaintenanceMode)
                    {
                        ServiceControlInstance.DisableMaintenanceMode();
                    }
                });

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
    }
}