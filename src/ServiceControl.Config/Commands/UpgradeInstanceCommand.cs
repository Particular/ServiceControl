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
            if (windowManager.ShowMessage("STOP INSTANCE AND UPGRADE", string.Format("{0} needs to be stopped in order to upgrade. Do you want to proceed.", instanceViewModel.Name)))
            {
                using (var progress = instanceViewModel.GetProgressObject("UPGRADING " + instanceViewModel.Name))
                {
                    var instance = ServiceControlInstance.FindByName(instanceViewModel.Name);

                    instance.Service.Refresh();
                    var isRunning = instance.Service.Status == ServiceControllerStatus.Running;
                    if (isRunning) await instanceViewModel.StopService();

                    var reportCard = await Task.Run(() => installer.Upgrade(instanceViewModel.Name, progress));

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