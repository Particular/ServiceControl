namespace ServiceControl.Config.UI.AdvanceOptions
{
    using System;
    using System.Linq;
    using System.ServiceProcess;
    using System.Threading.Tasks;
    using System.Windows.Input;
    using Caliburn.Micro;
    using ReactiveUI;
    using ServiceControl.Config.Commands;
    using ServiceControl.Config.Events;
    using ServiceControl.Config.Framework;
    using ServiceControl.Config.Framework.Rx;
    using ServiceControlInstaller.Engine.Instances;

    class AdvanceOptionsViewModel : RxProgressScreen, IHandle<RefreshInstances>
    {
        public AdvanceOptionsViewModel(ServiceControlInstance instance, IEventAggregator eventAggregator, StartServiceInMaintenanceModeCommand maintenanceModeCommand, DeleteInstanceCommand deleteInstanceCommand)
        {
            ServiceControlInstance = instance;
            DisplayName = "Advance Options";

            StartServiceInMaintenanceModeCommand = maintenanceModeCommand;
            DeleteCommand = deleteInstanceCommand;
            OpenUrl = new OpenURLCommand();
            CopyToClipboard = new CopyToClipboardCommand();
            StopMaintenanceModeCommand = new ReactiveCommand().DoAsync(async _ =>
            {
                await StopService();
                eventAggregator.PublishOnUIThread(new RefreshInstances());
            });
            Cancel = Command.Create(() =>
            {
                TryClose(false);
                eventAggregator.PublishOnUIThread(new RefreshInstances());
            }, () => !InProgress);
        }

        public ServiceControlInstance ServiceControlInstance { get; }

        public ICommand StartServiceInMaintenanceModeCommand { get; set; }
        public ICommand StopMaintenanceModeCommand { get; set; }

        public ICommand DeleteCommand { get; set; }

        public ICommand Cancel { get; set; }

        public ICommand OpenUrl { get; private set; }

        public ICommand CopyToClipboard { get; private set; }

        public string Name => ServiceControlInstance.Name;

        public bool InMaintenanceMode => ServiceControlInstance.InMaintenanceMode;

        public string StorageUrl => ServiceControlInstance.StorageUrl;

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

        public bool LaunchRavenStudio => AllowStop && InMaintenanceMode;

        public void Handle(RefreshInstances message)
        {
            NotifyOfPropertyChange("AllowStop");
            NotifyOfPropertyChange("IsRunning");
            NotifyOfPropertyChange("IsStopped");
            NotifyOfPropertyChange("InMaintenanceMode");
            NotifyOfPropertyChange("LaunchRavenStudio");
        }
    }
}