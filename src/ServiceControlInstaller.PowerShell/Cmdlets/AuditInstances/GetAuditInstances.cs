namespace ServiceControlInstaller.PowerShell
{
    using System.Linq;
    using System.Management.Automation;
    using Engine.Instances;

    [Cmdlet(VerbsCommon.Get, "AuditInstances")]
    public class GetAuditInstances : Cmdlet
    {
        protected override void ProcessRecord()
        {
            WriteObject(InstanceFinder.ServiceControlAuditInstances().Select(PsAuditInstance.FromInstance), true);
        }
    }
}
