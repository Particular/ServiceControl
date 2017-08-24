namespace ServiceControl.Config.Commands
{
    using System;
    using ServiceControl.Config.Framework;
    using ServiceControl.Config.Framework.Commands;
    using ServiceControl.Config.UI.AdvancedOptions;
    using ServiceControl.Config.UI.InstanceDetails;
    using ServiceControlInstaller.Engine.Instances;

    class AdvancedServiceControlOptionsCommand : AbstractCommand<InstanceDetailsViewModel>
    {
        private readonly Func<BaseService, ServiceControlAdvancedViewModel> advancedOptionsModel;
        private readonly IWindowManagerEx windowManager;

        public AdvancedServiceControlOptionsCommand(IWindowManagerEx windowManager, Func<BaseService, ServiceControlAdvancedViewModel> advancedOptionsModel)
        {
            this.windowManager = windowManager;
            this.advancedOptionsModel = advancedOptionsModel;
        }

        public override void Execute(InstanceDetailsViewModel viewModel)
        {
            var screen = advancedOptionsModel(viewModel.ServiceControlInstance);

            windowManager.ShowInnerDialog(screen);
        }
    }
}