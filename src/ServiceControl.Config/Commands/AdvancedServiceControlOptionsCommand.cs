namespace ServiceControl.Config.Commands
{
    using System;
    using Framework;
    using Framework.Commands;
    using ServiceControlInstaller.Engine.Instances;
    using UI.AdvancedOptions;
    using UI.InstanceDetails;

    class AdvancedServiceControlOptionsCommand : AbstractCommand<InstanceDetailsViewModel>
    {
        public AdvancedServiceControlOptionsCommand(IServiceControlWindowManager windowManager, Func<BaseService, ServiceControlAdvancedViewModel> advancedOptionsModel)
        {
            this.windowManager = windowManager;
            this.advancedOptionsModel = advancedOptionsModel;
        }

        public override void Execute(InstanceDetailsViewModel viewModel)
        {
            var screen = CreateAdvancedScreen(viewModel);
            windowManager.ShowInnerDialog(screen);
        }

        ServiceControlAdvancedViewModel CreateAdvancedScreen(InstanceDetailsViewModel viewModel)
        {
            switch (viewModel.InstanceType)
            {
                case InstanceType.ServiceControl:
                    return advancedOptionsModel(viewModel.ServiceControlInstance);
                case InstanceType.ServiceControlAudit:
                    return advancedOptionsModel(viewModel.ServiceControlAuditInstance);
                case InstanceType.Monitoring:
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        readonly Func<BaseService, ServiceControlAdvancedViewModel> advancedOptionsModel;
        readonly IServiceControlWindowManager windowManager;
    }
}