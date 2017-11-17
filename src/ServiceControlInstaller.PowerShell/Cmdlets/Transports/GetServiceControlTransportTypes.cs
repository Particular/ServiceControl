// ReSharper disable UnassignedField.Global
// ReSharper disable MemberCanBePrivate.Global
namespace ServiceControlInstaller.PowerShell
{
    using System.Linq;
    using System.Management.Automation;
    using ServiceControlInstaller.Engine.Instances;

    [Cmdlet(VerbsCommon.Get, "ServiceControlTransportTypes")]
    public class GetServiceControlTransportTypes : Cmdlet
    {
        protected override void ProcessRecord()
        {
            WriteObject(V5Transports.All.Select(PsTransportInfo.FromTransport), true);
        }
    }
}

