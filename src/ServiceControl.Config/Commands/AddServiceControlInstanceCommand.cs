namespace ServiceControl.Config.Commands
{
    using System;
    using System.Linq;
    using System.Threading;
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
            if (!await commandChecks.CanAddInstance(true))
            {
                return;
            }

            var instanceViewModel = addInstance();
            await windowManager.ShowInnerDialog(instanceViewModel);
        }

        public async Task ExecuteWithOptions(bool installError, bool installAudit, bool installServicePulse, CancellationToken cancellationToken = default)
        {
            if (!await commandChecks.CanAddInstance(true, cancellationToken))
            {
                return;
            }

            var instanceViewModel = addInstance();
            instanceViewModel.InstallErrorInstance = installError;
            instanceViewModel.InstallAuditInstance = installAudit;

            if (installError)
            {
                var spOption = installServicePulse
                    ? instanceViewModel.ServiceControl.EnableIntegratedServicePulseOptions.FirstOrDefault(o => o.Value)
                    : instanceViewModel.ServiceControl.EnableIntegratedServicePulseOptions.FirstOrDefault(o => !o.Value);

                if (spOption != null)
                {
                    instanceViewModel.ServiceControl.EnableIntegratedServicePulse = spOption;
                }
            }

            await windowManager.ShowInnerDialog(instanceViewModel, cancellationToken: cancellationToken);
        }

        readonly Func<ServiceControlAddViewModel> addInstance;
        readonly IServiceControlWindowManager windowManager;
        readonly ScmuCommandChecks commandChecks;
    }
}
