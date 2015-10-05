using System;
using ServiceControl.Config.Framework;
using ServiceControl.Config.Framework.Commands;
using ServiceControl.Config.UI.InstanceAdd;

namespace ServiceControl.Config.Commands
{
    class AddInstanceCommand : AbstractCommand<object>
    {
        private readonly Func<InstanceAddViewModel> addInstance;
        private readonly IWindowManagerEx windowManager;

        public AddInstanceCommand(IWindowManagerEx windowManager, Func<InstanceAddViewModel> addInstance) : base(null)
        {
            this.windowManager = windowManager;
            this.addInstance = addInstance;
        }

        public override void Execute(object obj)
        {
            var instanceViewModel = addInstance();

            windowManager.ShowInnerDialog(instanceViewModel);
        }
    }
}