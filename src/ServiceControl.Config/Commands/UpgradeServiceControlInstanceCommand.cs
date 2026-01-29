namespace ServiceControl.Config.Commands
{
    using System;
    using System.Threading.Tasks;
    using Caliburn.Micro;
    using Events;
    using FluentValidation;
    using Framework;
    using Framework.Commands;
    using Framework.Modules;
    using ServiceControlInstaller.Engine.Configuration.ServiceControl;
    using ServiceControlInstaller.Engine.Instances;
    using ServiceControlInstaller.Engine.ReportCard;
    using UI.InstanceDetails;
    using UI.MessageBox;
    using Validation;
    using Xaml.Controls;
    using Validations = Extensions.Validations;

    class UpgradeServiceControlInstanceCommand : AwaitableAbstractCommand<InstanceDetailsViewModel>
    {
        public UpgradeServiceControlInstanceCommand(Func<InstanceDetailsViewModel, bool> canExecuteMethod = null) : base(canExecuteMethod)
        {
        }

        public UpgradeServiceControlInstanceCommand(
            IServiceControlWindowManager windowManager,
            IEventAggregator eventAggregator,
            ServiceControlInstanceInstaller serviceControlInstaller,
            ScmuCommandChecks commandChecks
            )
        {
            this.windowManager = windowManager;
            this.eventAggregator = eventAggregator;
            this.serviceControlInstaller = serviceControlInstaller;
            this.commandChecks = commandChecks;
        }

        public override async Task ExecuteAsync(InstanceDetailsViewModel model)
        {
            var instance = InstanceFinder.FindInstanceByName<ServiceControlInstance>(model.Name);
            instance.Service.Refresh();

            if (!await commandChecks.CanUpgradeInstance(instance))
            {
                return;
            }

            var upgradeOptions = new ServiceControlUpgradeOptions();

            if (!instance.AppConfig.AppSettingExists(ServiceControlSettings.ForwardErrorMessages.Name))
            {
                var result = await windowManager.ShowYesNoCancelDialog("UPGRADE QUESTION - DISABLE ERROR FORWARDING", "Error messages can be forwarded to a secondary error queue known as the Error Forwarding Queue. This queue exists to allow external tools to receive error messages. If you do not have a tool processing messages from the Error Forwarding Queue this setting should be disabled.", "So what do you want to do ?", "Do NOT forward", "Yes I want to forward");
                if (!result.HasValue)
                {
                    //Dialog was cancelled
                    await eventAggregator.PublishOnUIThreadAsync(new RefreshInstances());
                    return;
                }

                upgradeOptions.OverrideEnableErrorForwarding = !result.Value;
            }

            //Grab old setting if it exists
            if (!instance.AppConfig.AppSettingExists(ServiceControlSettings.AuditRetentionPeriod.Name))
            {
                if (instance.AppConfig.AppSettingExists(ServiceControlSettings.HoursToKeepMessagesBeforeExpiring.Name))
                {
                    var i = instance.AppConfig.Read(ServiceControlSettings.HoursToKeepMessagesBeforeExpiring.Name, -1);
                    if (i != -1)
                    {
                        upgradeOptions.AuditRetentionPeriod = TimeSpan.FromHours(i);
                    }
                }
            }

            if (!instance.AppConfig.AppSettingExists(ServiceControlSettings.ErrorRetentionPeriod.Name))
            {
                var viewModel = new SliderDialogViewModel("INPUT REQUIRED - DATABASE RETENTION",
                    "Service Control periodically purges resolved and archived error messages from the database.",
                    "ERROR RETENTION PERIOD",
                    "Please specify the age at which these records should be removed",
                    TimeSpanUnits.Days,
                    SettingConstants.ErrorRetentionPeriodMinInDays,
                    SettingConstants.ErrorRetentionPeriodMaxInDays,
                    1,
                    1,
                    SettingConstants.ErrorRetentionPeriodDefaultInDaysForUI);

                if (await windowManager.ShowSliderDialog(viewModel))
                {
                    upgradeOptions.ErrorRetentionPeriod = viewModel.Period;
                }
                else
                {
                    //Dialog was cancelled
                    await eventAggregator.PublishOnUIThreadAsync(new RefreshInstances());
                    return;
                }
            }

            if (!instance.AppConfig.AppSettingExists(ServiceControlSettings.DatabaseMaintenancePort.Name))
            {
                var viewModel = new TextBoxDialogViewModel("INPUT REQUIRED - MAINTENANCE PORT",
                    "When Service Control is set to maintenance mode it requires a prereserved port on which it exposes the RavenDB database.",
                    "MAINTENANCE PORT",
                    "Please specify an open port that will be used as the maintenance port",
                    new PortValidator());

                if (await windowManager.ShowTextBoxDialog(viewModel))
                {
                    upgradeOptions.MaintenancePort = int.Parse(viewModel.Value);
                }
                else
                {
                    //Dialog was cancelled
                    await eventAggregator.PublishOnUIThreadAsync(new RefreshInstances());
                    return;
                }
            }

            if (!instance.AppConfig.AppSettingExists(ServiceControlSettings.EnableEmbeddedServicePulse.Name))
            {
                var result = await windowManager.ShowYesNoCancelDialog("INPUT REQUIRED - EMBEDDED SERVICEPULSE",
                    "ServiceControl can host an embedded version of ServicePulse which allows you to monitor your ServiceControl instance without needing to install ServicePulse separately.",
                    "Would you like to enable the embedded ServicePulse for this instance?",
                    "Enable Embedded ServicePulse",
                    "Do NOT enable Embedded ServicePulse");

                if (!result.HasValue)
                {
                    //Dialog was cancelled
                    await eventAggregator.PublishOnUIThreadAsync(new RefreshInstances());
                    return;
                }

                upgradeOptions.EnableEmbeddedServicePulse = result.Value;
            }

            if (await commandChecks.StopBecauseInstanceIsRunning(instance))
            {
                return;
            }

            await UpgradeServiceControlInstance(model, instance, upgradeOptions);

            await eventAggregator.PublishOnUIThreadAsync(new RefreshInstances());
        }

        async Task UpgradeServiceControlInstance(InstanceDetailsViewModel model, ServiceControlInstance instance, ServiceControlUpgradeOptions upgradeOptions)
        {
            using (var progress = model.GetProgressObject($"UPGRADING {model.Name}"))
            {
                var reportCard = new ReportCard();
                var restartAgain = model.IsRunning;

                var stopped = await model.StopService(progress);

                if (!stopped)
                {
                    await eventAggregator.PublishOnUIThreadAsync(new RefreshInstances());

                    reportCard.Errors.Add("Failed to stop the service");
                    reportCard.SetStatus();
                    await windowManager.ShowActionReport(reportCard, "ISSUES UPGRADING INSTANCE", "Could not upgrade instance because of the following errors:");

                    return;
                }

                reportCard = await Task.Run(() => serviceControlInstaller.Upgrade(instance, upgradeOptions, progress));

                if (reportCard.HasErrors || reportCard.HasWarnings)
                {
                    await windowManager.ShowActionReport(reportCard, "ISSUES UPGRADING INSTANCE", "Could not upgrade instance because of the following errors:", "There were some warnings while upgrading the instance:");
                }
                else
                {
                    if (restartAgain)
                    {
                        var serviceStarted = await model.StartService(progress);
                        if (!serviceStarted)
                        {
                            reportCard.Errors.Add("The Service failed to start. Please consult the ServiceControl logs for this instance");
                            await windowManager.ShowActionReport(reportCard, "UPGRADE FAILURE", "Instance reported this error after upgrade:");
                        }
                    }
                }
            }
        }

        readonly IEventAggregator eventAggregator;
        readonly IServiceControlWindowManager windowManager;
        readonly ServiceControlInstanceInstaller serviceControlInstaller;
        readonly ScmuCommandChecks commandChecks;

        class PortValidator : AbstractValidator<TextBoxDialogViewModel>
        {
            public PortValidator()
            {
                RuleFor(x => x.Value)
                    .NotEmpty()
                    .ValidPort()
                    .MustNotBeIn(x => Validations.UsedPorts())
                    .WithMessage(string.Format(Validation.Validations.MSG_MUST_BE_UNIQUE, "Ports"));
            }
        }
    }
}