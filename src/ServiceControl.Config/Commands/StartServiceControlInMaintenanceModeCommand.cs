namespace ServiceControl.Config.Commands
{
    using System.Threading.Tasks;
    using Caliburn.Micro;
    using Events;
    using Framework;
    using Framework.Commands;
    using ServiceControlInstaller.Engine.ReportCard;
    using UI.AdvancedOptions;

    class StartServiceControlInMaintenanceModeCommand : AwaitableAbstractCommand<ServiceControlAdvancedViewModel>
    {
        public StartServiceControlInMaintenanceModeCommand(IWindowManagerEx windowManager, IEventAggregator eventAggregator) : base(model => !(model.IsRunning && model.InMaintenanceMode))
        {
            this.windowManager = windowManager;
            this.eventAggregator = eventAggregator;
        }

        public override async Task ExecuteAsync(ServiceControlAdvancedViewModel model)
        {
            model.ServiceControlInstance.Service.Refresh();

            var confirm = model.IsStopped ||
                          windowManager.ShowYesNoDialog("STOP INSTANCE AND START IN MAINTENANCE MODE", $"{model.Name} needs to be stopped in order to start in Maintenance Mode.", "Do you want to proceed?", "Yes I want to proceed", "No");

            if (confirm)
            {
                using (var progress = model.GetProgressObject())
                {
                    var stopped = await model.StopService(progress);

                    if (!stopped)
                    {
                        eventAggregator.PublishOnUIThread(new RefreshInstances());
                        var reportCard = new ReportCard();
                        reportCard.Errors.Add("Failed to stop the service");
                        reportCard.SetStatus();
                        windowManager.ShowActionReport(reportCard, "ISSUES STARTING INSTANCE IN MAINTENANCE MODE", "There were some errors when attempting to start instance in Maintenance Mode:");
                        return;
                    }

                    var started = await model.StartServiceInMaintenanceMode(progress);

                    if (!started)
                    {
                        var reportCard = new ReportCard();
                        reportCard.Warnings.Add("Failed to start the service");
                        reportCard.SetStatus();
                        windowManager.ShowActionReport(reportCard, "ISSUES STARTING INSTANCE IN MAINTENANCE MODE", "There were some warnings when attempting to start instance in Maintenance Mode:");
                    }
                }

                eventAggregator.PublishOnUIThread(new RefreshInstances());
            }
        }

        readonly IWindowManagerEx windowManager;
        readonly IEventAggregator eventAggregator;
    }
}