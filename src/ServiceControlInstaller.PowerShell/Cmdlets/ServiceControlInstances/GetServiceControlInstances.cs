// ReSharper disable UnassignedField.Global
// ReSharper disable MemberCanBePrivate.Global

namespace ServiceControlInstaller.PowerShell
{
    using System.Linq;
    using System.Management.Automation;
    using Engine.Instances;

    [Cmdlet(VerbsCommon.Get, "ServiceControlInstances")]
    public class GetServiceControlInstances : Cmdlet
    {
        protected override void ProcessRecord()
        {
            WriteObject(InstanceFinder.ServiceControlInstances().Select(PsServiceControl.FromInstance), true);
        }
    }
}