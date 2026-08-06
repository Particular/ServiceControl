namespace ServiceControl.Config.Commands
{
    using System;
    using System.Threading.Tasks;
    using Caliburn.Micro;
    using Events;
    using Framework;
    using Framework.Commands;
    using ServiceControlInstaller.Engine.Instances;
    using UI.AdvancedOptions;
    using UI.InstanceDetails;

    class AdvancedMonitoringOptionsCommand : AwaitableAbstractCommand<InstanceDetailsViewModel>
    {
        public AdvancedMonitoringOptionsCommand(IServiceControlWindowManager windowManager, IEventAggregator eventAggregator, Func<BaseService, MonitoringAdvancedViewModel> advancedOptionsModel)
        {
            this.windowManager = windowManager;
            this.eventAggregator = eventAggregator;
            this.advancedOptionsModel = advancedOptionsModel;
        }

        public override async Task ExecuteAsync(InstanceDetailsViewModel viewModel)
        {
            var screen = advancedOptionsModel(viewModel.MonitoringInstance);
            await windowManager.ShowInnerDialog(screen);
            await eventAggregator.PublishOnUIThreadAsync(new RefreshInstances());
        }

        readonly Func<BaseService, MonitoringAdvancedViewModel> advancedOptionsModel;
        readonly IEventAggregator eventAggregator;
        readonly IServiceControlWindowManager windowManager;
    }
}