namespace ServiceControl.Config.Commands
{
    using System;
    using System.Threading.Tasks;
    using Framework;
    using Framework.Commands;
    using UI.InstanceAdd;

    class AddServiceControlInstanceCommand : AwaitableAbstractCommand<object>
    {
        public AddServiceControlInstanceCommand(IServiceControlWindowManager windowManager, Func<ServiceControlAddViewModel> addInstance, ScmuCommandChecks commandChecks)
            : base(null)
        {
            this.windowManager = windowManager;
            this.addInstance = addInstance;
            this.commandChecks = commandChecks;
        }

        public override async Task ExecuteAsync(object obj)
        {
            if (!await commandChecks.CanAddInstance())
            {
                return;
            }

            var instanceViewModel = addInstance();
            await windowManager.ShowInnerDialog(instanceViewModel);
        }

        readonly Func<ServiceControlAddViewModel> addInstance;
        readonly IServiceControlWindowManager windowManager;
        readonly ScmuCommandChecks commandChecks;
    }
}