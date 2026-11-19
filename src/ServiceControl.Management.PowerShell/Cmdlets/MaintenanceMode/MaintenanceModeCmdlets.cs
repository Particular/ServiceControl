namespace ServiceControl.Management.PowerShell
{
    using System;
    using System.Linq;
    using System.Management.Automation;
    using ServiceControlInstaller.Engine.Instances;

    [Cmdlet(VerbsLifecycle.Enable, "ServiceControlMaintenanceMode")]
    public class EnableServiceControlMaintenanceMode : MaintenanceModeCmdletBase
    {
        protected override bool Enable => true;

        protected override ServiceControlBaseService FindInstance(string name) =>
            InstanceFinder.ServiceControlInstances().FirstOrDefault(i => i.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    [Cmdlet(VerbsLifecycle.Disable, "ServiceControlMaintenanceMode")]
    public class DisableServiceControlMaintenanceMode : MaintenanceModeCmdletBase
    {
        protected override bool Enable => false;

        protected override ServiceControlBaseService FindInstance(string name) =>
            InstanceFinder.ServiceControlInstances().FirstOrDefault(i => i.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    [Cmdlet(VerbsLifecycle.Enable, "ServiceControlAuditMaintenanceMode")]
    public class EnableServiceControlAuditMaintenanceMode : MaintenanceModeCmdletBase
    {
        protected override bool Enable => true;

        protected override ServiceControlBaseService FindInstance(string name) =>
            InstanceFinder.ServiceControlAuditInstances().FirstOrDefault(i => i.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    [Cmdlet(VerbsLifecycle.Disable, "ServiceControlAuditMaintenanceMode")]
    public class DisableServiceControlAuditMaintenanceMode : MaintenanceModeCmdletBase
    {
        protected override bool Enable => false;

        protected override ServiceControlBaseService FindInstance(string name) =>
            InstanceFinder.ServiceControlAuditInstances().FirstOrDefault(i => i.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }
}
