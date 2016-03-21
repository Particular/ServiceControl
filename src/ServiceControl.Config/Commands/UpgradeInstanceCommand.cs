namespace ServiceControl.Config.Commands
{
    using System.ServiceProcess;
    using System.Threading.Tasks;
    using Caliburn.Micro;
    using Framework;
    using Framework.Commands;
    using ServiceControl.Config.Events;
    using ServiceControl.Config.Framework.Modules;
    using ServiceControl.Config.UI.InstanceDetails;
    using ServiceControlInstaller.Engine.Instances;

    class UpgradeInstanceCommand : AwaitableAbstractCommand<InstanceDetailsViewModel>
    {
        private readonly IEventAggregator eventAggregator;
        private readonly Installer installer;
        private readonly IWindowManagerEx windowManager;

        public UpgradeInstanceCommand(IWindowManagerEx windowManager, IEventAggregator eventAggregator, Installer installer)
        {
            this.windowManager = windowManager;

            this.eventAggregator = eventAggregator;
            this.installer = installer;
        }

        public override async Task ExecuteAsync(InstanceDetailsViewModel instanceViewModel)
        {
            var instance = ServiceControlInstance.FindByName(instanceViewModel.Name);
            instance.Service.Refresh();
            bool? overrideErrorForwarding = null;

            if (!instance.AppSettingExists("ServiceControl/ForwardErrorMessages"))
            {

                var result  = windowManager.ShowYesNoCancelDialog("DISABLE ERROR FORWARDING?", "Error messages can be forwarded to a secondary error queue known as the Error Forwarding Queue.  This queue exists to allow external tools to receive error messages. If you do not have a tool processing messages from the Error Forwarding Queue this setting should be disabled.", "Disable", "Enable");
                if (!result.HasValue)
                {
                    //Dialog was cancelled
                    eventAggregator.PublishOnUIThread(new RefreshInstances());
                    return;
                }
                overrideErrorForwarding = !result.Value;
            }
            
            var confirm = instance.Service.Status == ServiceControllerStatus.Stopped ||
                windowManager.ShowMessage(string.Format("STOP INSTANCE AND UPGRADE TO {0}", installer.ZipInfo.Version), string.Format("{0} needs to be stopped in order to upgrade to version {1}. Do you want to proceed?", instanceViewModel.Name, installer.ZipInfo.Version));

            if (confirm)
            {
                using (var progress = instanceViewModel.GetProgressObject("UPGRADING " + instanceViewModel.Name))
                {
                    instance.Service.Refresh();
                    var isRunning = instance.Service.Status == ServiceControllerStatus.Running;
                    if (isRunning) await instanceViewModel.StopService();

                    var reportCard = await Task.Run(() => installer.Upgrade(instanceViewModel.Name, overrideErrorForwarding, progress));

                    if (reportCard.HasErrors || reportCard.HasWarnings)
                    {
                        windowManager.ShowActionReport(reportCard, "ISSUES UPGRADING INSTANCE", "Could not upgrade instance because of the following errors:", "There were some warnings while upgrading  the instance:");
                    }
                    else
                    {
                        if (isRunning) await instanceViewModel.StartService();
                    }
                }
            }

            eventAggregator.PublishOnUIThread(new RefreshInstances());
        }
    }
}