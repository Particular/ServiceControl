﻿namespace ServiceControl.Config.Commands
{
    using System;
    using System.Threading.Tasks;
    using Caliburn.Micro;
    using Events;
    using Framework;
    using Framework.Commands;
    using Framework.Modules;
    using UI.AdvancedOptions;
    using UI.DeleteInstanceConfirmation;

    class DeleteServiceControlInstanceCommand : AwaitableAbstractCommand<ServiceControlAdvancedViewModel>
    {
        public DeleteServiceControlInstanceCommand(IServiceControlWindowManager windowManager, IEventAggregator eventAggregator, ServiceControlInstanceInstaller installer, Func<DeleteServiceControlConfirmationViewModel> deleteInstanceConfirmation) : base(model => model != null)
        {
            this.windowManager = windowManager;
            this.deleteInstanceConfirmation = deleteInstanceConfirmation;
            this.eventAggregator = eventAggregator;
            this.installer = installer;
        }

        public override async Task ExecuteAsync(ServiceControlAdvancedViewModel model)
        {
            var instanceVersion = model.ServiceControlInstance.Version;

            if (await InstallerVersionCompatibilityDialog.ShowValidation(instanceVersion, windowManager))
            {
                return;
            }

            var confirmation = deleteInstanceConfirmation();
            confirmation.InstanceName = model.Name;
            var isConfirmed = await windowManager.ShowDialogAsync(confirmation);
            if (isConfirmed == true)
            {
                using (var progress = model.GetProgressObject("REMOVING " + model.Name))
                {
                    var reportCard = await Task.Run(() => installer.Delete(model.Name, confirmation.RemoveDatabase, confirmation.RemoveLogs, progress));

                    if (reportCard.HasErrors || reportCard.HasWarnings)
                    {
                        await windowManager.ShowActionReport(reportCard, "ISSUES REMOVING INSTANCE", "Could not remove instance because of the following errors:", "There were some warnings while deleting the instance:");
                    }
                    else
                    {
                        await model.TryCloseAsync(true);
                    }
                }

                await eventAggregator.PublishOnUIThreadAsync(new ResetInstances());
            }
        }

        readonly Func<DeleteServiceControlConfirmationViewModel> deleteInstanceConfirmation;
        readonly IEventAggregator eventAggregator;
        readonly ServiceControlInstanceInstaller installer;
        readonly IServiceControlWindowManager windowManager;
    }
}