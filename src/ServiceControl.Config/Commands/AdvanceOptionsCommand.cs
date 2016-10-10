namespace ServiceControl.Config.Commands
{
    using System;
    using ServiceControl.Config.Framework;
    using ServiceControl.Config.Framework.Commands;
    using ServiceControl.Config.UI.AdvancedOptions;
    using ServiceControl.Config.UI.InstanceDetails;
    using ServiceControlInstaller.Engine.Instances;

    class AdvanceOptionsCommand : AbstractCommand<InstanceDetailsViewModel>
    {
        private readonly Func<ServiceControlInstance, AdvancedOptionsViewModel> advancedOptionsModel;
        private readonly IWindowManagerEx windowManager;

        public AdvanceOptionsCommand(IWindowManagerEx windowManager, Func<ServiceControlInstance, AdvancedOptionsViewModel> advancedOptionsModel)
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