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

    class DeleteMonitoringlnstanceCommand : AwaitableAbstractCommand<MonitoringAdvancedViewModel>
    {
        public DeleteMonitoringlnstanceCommand(IServiceControlWindowManager windowManager, IEventAggregator eventAggregator, MonitoringInstanceInstaller installer, Func<DeleteMonitoringConfirmationViewModel> deleteInstanceConfirmation) : base(model => model != null)
        {
            this.windowManager = windowManager;
            this.deleteInstanceConfirmation = deleteInstanceConfirmation;
            this.eventAggregator = eventAggregator;
            this.installer = installer;
        }

        public override async Task ExecuteAsync(MonitoringAdvancedViewModel model)
        {
            var instanceVersion = model.MonitoringInstance.Version;
            var installerVersion = installer.ZipInfo.Version;

            if (await InstallerVersionCompatibilityDialog.ShowValidation(instanceVersion, installerVersion, windowManager))
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
                    var reportCard = await Task.Run(() => installer.Delete(model.Name, confirmation.RemoveLogs, progress));

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

        readonly Func<DeleteMonitoringConfirmationViewModel> deleteInstanceConfirmation;
        readonly IEventAggregator eventAggregator;
        readonly MonitoringInstanceInstaller installer;
        readonly IServiceControlWindowManager windowManager;
    }
}