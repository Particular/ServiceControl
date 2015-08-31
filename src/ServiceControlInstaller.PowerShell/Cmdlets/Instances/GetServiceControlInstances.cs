// ReSharper disable UnassignedField.Global
// ReSharper disable MemberCanBePrivate.Global
namespace ServiceControlInstaller.PowerShell
{
    using System.Management.Automation;
    using ServiceControlInstaller.Engine.Instances;

    [Cmdlet(VerbsCommon.Get, "ServiceControlInstances")]
    public class GetServiceControlInstances : Cmdlet
    {
        protected override void ProcessRecord()
        {
            WriteObject(ServiceControlInstance.Instances(), true);
        }
    }
}