using System;
using System.Threading.Tasks;
using Caliburn.Micro;
using ServiceControl.Config.Events;
using ServiceControl.Config.Framework;
using ServiceControl.Config.Framework.Commands;
using ServiceControl.Config.UI.DeleteInstanceConfirmation;
using ServiceControl.Config.UI.InstanceDetails;

namespace ServiceControl.Config.Commands
{
    using ServiceControl.Config.Framework.Modules;

    class DeleteInstanceCommand : AwaitableAbstractCommand<InstanceDetailsViewModel>
    {
        private readonly Func<DeleteInstanceConfirmationViewModel> deleteInstanceConfirmation;
        private readonly IEventAggregator eventAggregator;
        private readonly Installer installer;
        private readonly IWindowManagerEx windowManager;

        public DeleteInstanceCommand(IWindowManagerEx windowManager, IEventAggregator eventAggregator, Installer installer, Func<DeleteInstanceConfirmationViewModel> deleteInstanceConfirmation)
        {
            this.windowManager = windowManager;
            this.deleteInstanceConfirmation = deleteInstanceConfirmation;
            this.eventAggregator = eventAggregator;
            this.installer = installer;
        }

        public override async Task ExecuteAsync(InstanceDetailsViewModel instanceViewModel)
        {
            var confirmation = deleteInstanceConfirmation();
            confirmation.InstanceName = instanceViewModel.Name;
            if (windowManager.ShowDialog(confirmation) == true)
            {
                using (var progress = instanceViewModel.GetProgressObject("REMOVING " + instanceViewModel.Name))
                {
                    var reportCard = await Task.Run(() => installer.Delete(instanceViewModel.Name, confirmation.RemoveDatabase, confirmation.RemoveLogs, progress));

                    if (reportCard.HasErrors || reportCard.HasWarnings)
                    {
                        windowManager.ShowActionReport(reportCard, "ISSUES REMOVING INSTANCE", "Could not remove instance because of the following errors:", "There were some warnings while deleting the instance:");
                    }
                }

                eventAggregator.PublishOnUIThread(new RefreshInstances());
            }
        }
    }
}