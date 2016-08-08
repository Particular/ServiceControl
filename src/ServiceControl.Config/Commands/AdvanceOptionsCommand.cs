namespace ServiceControl.Config.Commands
{
    using System;
    using ServiceControl.Config.Framework;
    using ServiceControl.Config.Framework.Commands;
    using ServiceControl.Config.UI.AdvanceOptions;
    using ServiceControl.Config.UI.InstanceDetails;
    using ServiceControlInstaller.Engine.Instances;

    class AdvanceOptionsCommand : AbstractCommand<InstanceDetailsViewModel>
    {
        private readonly Func<ServiceControlInstance, AdvanceOptionsViewModel> advanceOptionsModel;
        private readonly IWindowManagerEx windowManager;

        public AdvanceOptionsCommand(IWindowManagerEx windowManager, Func<ServiceControlInstance, AdvanceOptionsViewModel> advanceOptionsModel)
        {
            this.windowManager = windowManager;
            this.advanceOptionsModel = advanceOptionsModel;
        }

        public override void Execute(InstanceDetailsViewModel viewModel)
        {
            var screen = advanceOptionsModel(viewModel.ServiceControlInstance);

            windowManager.ShowInnerDialog(screen);
        }
    }
}