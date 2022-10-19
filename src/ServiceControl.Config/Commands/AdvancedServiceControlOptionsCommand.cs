namespace ServiceControl.Config.Commands
{
    using System;
    using System.Threading.Tasks;
    using Framework;
    using Framework.Commands;
    using ServiceControlInstaller.Engine.Instances;
    using UI.AdvancedOptions;
    using UI.InstanceDetails;

    class AdvancedServiceControlOptionsCommand : AwaitableAbstractCommand<InstanceDetailsViewModel>
    {
        public AdvancedServiceControlOptionsCommand(IServiceControlWindowManager windowManager, Func<BaseService, ServiceControlAdvancedViewModel> advancedOptionsModel)
        {
            this.windowManager = windowManager;
            this.advancedOptionsModel = advancedOptionsModel;
        }

        public override Task ExecuteAsync(InstanceDetailsViewModel viewModel)
        {
            var screen = CreateAdvancedScreen(viewModel);
            return windowManager.ShowInnerDialog(screen);
        }

        ServiceControlAdvancedViewModel CreateAdvancedScreen(InstanceDetailsViewModel viewModel) =>
            viewModel.InstanceType switch
            {
                InstanceType.ServiceControl => advancedOptionsModel(viewModel.ServiceControlInstance),
                InstanceType.ServiceControlAudit => advancedOptionsModel(viewModel.ServiceControlAuditInstance),
                InstanceType.Monitoring => throw new ArgumentOutOfRangeException(),
                _ => throw new ArgumentOutOfRangeException()
            };

        readonly Func<BaseService, ServiceControlAdvancedViewModel> advancedOptionsModel;
        readonly IServiceControlWindowManager windowManager;
    }
}