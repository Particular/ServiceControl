namespace ServiceControl.Config.Commands
{
    using System;
    using ServiceControl.Config.Framework;
    using ServiceControl.Config.Framework.Commands;
    using ServiceControl.Config.UI.InstanceDetails;
    using ServiceControl.Config.UI.InstanceEdit;
    using ServiceControlInstaller.Engine.Instances;

    class EditInstanceCommand : AbstractCommand<InstanceDetailsViewModel>
    {
        private readonly Func<ServiceControlInstance, InstanceEditViewModel> editViewModel;
        private readonly IWindowManagerEx windowManager;

        public EditInstanceCommand(IWindowManagerEx windowManager, Func<ServiceControlInstance, InstanceEditViewModel> editViewModel) : base(null)
        {
            this.windowManager = windowManager;
            this.editViewModel = editViewModel;
        }

        public override void Execute(InstanceDetailsViewModel viewModel)
        {
            var editVM = editViewModel(viewModel.ServiceControlInstance);

            windowManager.ShowInnerDialog(editVM);
        }
    }
}