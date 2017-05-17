namespace ServiceControl.Config.Commands
{
    using System;
    using ServiceControl.Config.Framework;
    using ServiceControl.Config.Framework.Commands;
    using ServiceControl.Config.UI.InstanceDetails;
    using ServiceControl.Config.UI.InstanceEdit;
    using ServiceControlInstaller.Engine.Instances;
    
    class EditServiceControlInstanceCommand : AbstractCommand<InstanceDetailsViewModel>
    {
        private readonly Func<ServiceControlInstance, ServiceControlEditViewModel> editViewModel;
        private readonly IWindowManagerEx windowManager;

        public EditServiceControlInstanceCommand(IWindowManagerEx windowManager, Func<ServiceControlInstance, ServiceControlEditViewModel> editViewModel) : base(null)
        {
            this.windowManager = windowManager;
            this.editViewModel = editViewModel;
        }

        public override void Execute(InstanceDetailsViewModel viewModel)
        {
            var editVM = editViewModel((ServiceControlInstance)viewModel.ServiceInstance);

            windowManager.ShowInnerDialog(editVM);
        }
    }
}