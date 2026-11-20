namespace ServiceControl.Management.PowerShell
{
    using System;
    using System.Linq;
    using System.Management.Automation;
    using ServiceControlInstaller.Engine.Instances;

    [Cmdlet(VerbsLifecycle.Start, "ServiceControlMaintenanceMode")]
    public class StartServiceControlMaintenanceMode : MaintenanceModeCmdletBase
    {
        protected override bool StartInMaintenanceMode => true;

        protected override ServiceControlBaseService FindInstance(string name) =>
            InstanceFinder.ServiceControlInstances().FirstOrDefault(i => i.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    [Cmdlet(VerbsLifecycle.Stop, "ServiceControlMaintenanceMode")]
    public class StopServiceControlMaintenanceMode : MaintenanceModeCmdletBase
    {
        protected override bool StartInMaintenanceMode => false;

        protected override ServiceControlBaseService FindInstance(string name) =>
            InstanceFinder.ServiceControlInstances().FirstOrDefault(i => i.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    [Cmdlet(VerbsLifecycle.Start, "ServiceControlAuditMaintenanceMode")]
    public class StartServiceControlAuditMaintenanceMode : MaintenanceModeCmdletBase
    {
        protected override bool StartInMaintenanceMode => true;

        protected override ServiceControlBaseService FindInstance(string name) =>
            InstanceFinder.ServiceControlAuditInstances().FirstOrDefault(i => i.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    [Cmdlet(VerbsLifecycle.Stop, "ServiceControlAuditMaintenanceMode")]
    public class StopServiceControlAuditMaintenanceMode : MaintenanceModeCmdletBase
    {
        protected override bool StartInMaintenanceMode => false;

        protected override ServiceControlBaseService FindInstance(string name) =>
            InstanceFinder.ServiceControlAuditInstances().FirstOrDefault(i => i.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }
}
