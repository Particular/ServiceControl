namespace ServiceControl.Config.UI.AdvancedOptions
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
    using ServiceControlInstaller.Engine.Configuration;
    using ServiceControlInstaller.Engine.Instances;

    class AdvancedOptionsViewModel : RxProgressScreen, IHandle<RefreshInstances>
    {

        public AdvancedOptionsViewModel(ServiceControlInstance instance,
            IEventAggregator eventAggregator,
            StartServiceInMaintenanceModeCommand maintenanceModeCommand,
            DeleteInstanceCommand deleteInstanceCommand,
            IWindowManagerEx windowManager
            )
        {
            this.eventAggregator = eventAggregator;
            ServiceControlInstance = instance;
            DisplayName = "ADVANCED OPTIONS";

            StartServiceInMaintenanceModeCommand = new ReactiveCommand().DoAsync(async _ =>
            {
                await  maintenanceModeCommand.ExecuteAsync(this);
                eventAggregator.PublishOnUIThread(new RefreshInstances());
            });
            DeleteCommand = deleteInstanceCommand;
            OpenUrl = new OpenURLCommand();
            CopyToClipboard = new CopyToClipboardCommand();
            OpenCertDialogCommand = new ReactiveCommand().DoAction(obj =>
            {
                var sslConfig = new CertificateDialogViewModel(this.eventAggregator, ServiceControlInstance);
                windowManager.ShowDialog(sslConfig);
            });

            StopMaintenanceModeCommand = new ReactiveCommand().DoAsync(async _ =>
            {
                await StopService();
                eventAggregator.PublishOnUIThread(new RefreshInstances());
            });
            Cancel = Command.Create(() =>
            {
                TryClose(false);
            }, () => !InProgress);
        }

        private IEventAggregator eventAggregator;

        public ServiceControlInstance ServiceControlInstance { get; }

        public ICommand StartServiceInMaintenanceModeCommand { get; set; }
        public ICommand StopMaintenanceModeCommand { get; set; }

        public bool MaintenanceModeSupported => ServiceControlInstance.Version >= SettingsList.MaintenanceMode.SupportedFrom;

        public bool SslSupported => ServiceControlInstance.Version >= Version.Parse("1.22.1");

        public ICommand DeleteCommand { get; set; }

        public ICommand OpenCertDialogCommand{ get; private set; }

        public ICommand Cancel { get; set; }

        public ICommand OpenUrl { get; private set; }

        public ICommand CopyToClipboard { get; private set; }

        public string Name => ServiceControlInstance.Name;

        public bool InMaintenanceMode => ServiceControlInstance.InMaintenanceMode;

        public string StorageUrl => ServiceControlInstance.StorageUrl;

        public async Task<bool> StartServiceInMaintenanceMode(IProgressObject progress)
        {
            var disposeProgress = progress == null;
            var result = false;
            try
            {
                progress = progress ?? this.GetProgressObject();

                progress.Report(new ProgressDetails("Starting Service"));
                await Task.Run(() => {
                    ServiceControlInstance.EnableMaintenanceMode();
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
                progress = progress ?? this.GetProgressObject();

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

        public void Handle(RefreshInstances message)
        {
            NotifyOfPropertyChange("AllowStop");
            NotifyOfPropertyChange("IsRunning");
            NotifyOfPropertyChange("IsStopped");
            NotifyOfPropertyChange("InMaintenanceMode");
         }


    }
}