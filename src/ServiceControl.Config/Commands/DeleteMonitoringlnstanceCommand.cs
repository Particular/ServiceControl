namespace ServiceControl.Config.Commands
{
    using System;
    using System.Threading.Tasks;
    using Caliburn.Micro;
    using ServiceControl.Config.Events;
    using ServiceControl.Config.Framework;
    using ServiceControl.Config.Framework.Commands;
    using ServiceControl.Config.Framework.Modules;
    using ServiceControl.Config.UI.AdvancedOptions;
    using ServiceControl.Config.UI.DeleteInstanceConfirmation;

    class DeleteMonitoringlnstanceCommand : AwaitableAbstractCommand<MonitoringAdvancedViewModel>
    {
        private readonly Func<DeleteMonitoringConfirmationViewModel> deleteInstanceConfirmation;
        private readonly IEventAggregator eventAggregator;
        private readonly MonitoringInstanceInstaller installer;
        private readonly IWindowManagerEx windowManager;

        public DeleteMonitoringlnstanceCommand(IWindowManagerEx windowManager, IEventAggregator eventAggregator, MonitoringInstanceInstaller installer, Func<DeleteMonitoringConfirmationViewModel> deleteInstanceConfirmation) : base(model => model != null)
        {
            this.windowManager = windowManager;
            this.deleteInstanceConfirmation = deleteInstanceConfirmation;
            this.eventAggregator = eventAggregator;
            this.installer = installer;
        }

        public override async Task ExecuteAsync(MonitoringAdvancedViewModel model)
        {
            var confirmation = deleteInstanceConfirmation();
            confirmation.InstanceName = model.Name;
            if (windowManager.ShowDialog(confirmation) == true)
            {
                using (var progress = model.GetProgressObject("REMOVING " + model.Name))
                {
                    var reportCard = await Task.Run(() => installer.Delete(model.Name, confirmation.RemoveLogs, progress));

                    if (reportCard.HasErrors || reportCard.HasWarnings)
                    {
                        windowManager.ShowActionReport(reportCard, "ISSUES REMOVING INSTANCE", "Could not remove instance because of the following errors:", "There were some warnings while deleting the instance:");
                    }
                    else
                    {
                        model.TryClose(true);
                    }
                }
                eventAggregator.PublishOnUIThread(new ResetInstances());
            }
        }
    }
}