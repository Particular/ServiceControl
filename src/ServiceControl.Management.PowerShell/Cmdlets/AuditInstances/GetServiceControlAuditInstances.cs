namespace ServiceControl.Management.PowerShell
{
    using System.Linq;
    using System.Management.Automation;
    using ServiceControlInstaller.Engine.Instances;

    [Cmdlet(VerbsCommon.Get, "ServiceControlAuditInstances")]
    public class GetServiceControlAuditInstances : Cmdlet
    {
        protected override void ProcessRecord()
        {
            WriteObject(InstanceFinder.ServiceControlAuditInstances().Select(PsAuditInstance.FromInstance), true);
        }
    }
}