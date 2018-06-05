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

    class DeleteServiceControlInstanceCommand : AwaitableAbstractCommand<ServiceControlAdvancedViewModel>
    {
        private readonly Func<DeleteServiceControlConfirmationViewModel> deleteInstanceConfirmation;
        private readonly IEventAggregator eventAggregator;
        private readonly ServiceControlInstanceInstaller installer;
        private readonly IWindowManagerEx windowManager;

        public DeleteServiceControlInstanceCommand(IWindowManagerEx windowManager, IEventAggregator eventAggregator, ServiceControlInstanceInstaller installer, Func<DeleteServiceControlConfirmationViewModel> deleteInstanceConfirmation) : base(model => model != null)
        {
            this.windowManager = windowManager;
            this.deleteInstanceConfirmation = deleteInstanceConfirmation;
            this.eventAggregator = eventAggregator;
            this.installer = installer;
        }

        public override async Task ExecuteAsync(ServiceControlAdvancedViewModel model)
        {
            var confirmation = deleteInstanceConfirmation();
            confirmation.InstanceName = model.Name;
            if (windowManager.ShowDialog(confirmation) == true)
            {
                using (var progress = model.GetProgressObject("REMOVING " + model.Name))
                {
                    var reportCard = await Task.Run(() => installer.Delete(model.Name, confirmation.RemoveDatabase, confirmation.RemoveLogs, progress));

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