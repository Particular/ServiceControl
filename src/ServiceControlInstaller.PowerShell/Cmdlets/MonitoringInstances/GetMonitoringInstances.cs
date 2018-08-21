namespace ServiceControlInstaller.PowerShell
{
    using System.Linq;
    using System.Management.Automation;
    using Cmdlets.Instances;
    using Engine.Instances;

    [Cmdlet(VerbsCommon.Get, "MonitoringInstances")]
    public class GetMonitoringInstances : Cmdlet
    {
        protected override void ProcessRecord()
        {
            WriteObject(InstanceFinder.MonitoringInstances().Select(PsMonitoringService.FromInstance), true);
        }
    }
}