namespace ServiceControlInstaller.PowerShell
{
    using System.Linq;
    using System.Management.Automation;
    using ServiceControlInstaller.Engine.Instances;
    using ServiceControlInstaller.PowerShell.Cmdlets.Instances;

    [Cmdlet(VerbsCommon.Get, "MonitoringInstances")]
    public class GetMonitoringInstances : Cmdlet
    {
        protected override void ProcessRecord()
        {
            WriteObject(InstanceFinder.MonitoringInstances().Select(PsMonitoringService.FromInstance), true);
        }
    }
}