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

    class AdvancedServiceControlOptionsCommand : AwaitableAbstractCommand<InstanceDetailsViewModel>
    {
        public AdvancedServiceControlOptionsCommand(IServiceControlWindowManager windowManager, IEventAggregator eventAggregator, Func<BaseService, ServiceControlAdvancedViewModel> advancedOptionsModel)
        {
            this.windowManager = windowManager;
            this.eventAggregator = eventAggregator;
            this.advancedOptionsModel = advancedOptionsModel;
        }

        public override async Task ExecuteAsync(InstanceDetailsViewModel viewModel)
        {
            var screen = CreateAdvancedScreen(viewModel);
            await windowManager.ShowInnerDialog(screen);
            await eventAggregator.PublishOnUIThreadAsync(new RefreshInstances());
        }

        ServiceControlAdvancedViewModel CreateAdvancedScreen(InstanceDetailsViewModel viewModel)
        {
            return viewModel.InstanceType switch
            {
                InstanceType.ServiceControl => advancedOptionsModel(viewModel.ServiceControlInstance),
                InstanceType.ServiceControlAudit => advancedOptionsModel(viewModel.ServiceControlAuditInstance),
                InstanceType.Monitoring => throw new NotImplementedException(),
                _ => throw new ArgumentOutOfRangeException(),
            };
        }

        readonly Func<BaseService, ServiceControlAdvancedViewModel> advancedOptionsModel;
        readonly IEventAggregator eventAggregator;
        readonly IServiceControlWindowManager windowManager;
    }
}