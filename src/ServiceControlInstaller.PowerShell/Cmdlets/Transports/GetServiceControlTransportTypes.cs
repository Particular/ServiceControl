namespace ServiceControlInstaller.PowerShell
{
    using System.Linq;
    using System.Management.Automation;
    using Engine.Instances;

    [Cmdlet(VerbsCommon.Get, "ServiceControlTransportTypes")]
    public class GetServiceControlTransportTypes : Cmdlet
    {
        protected override void ProcessRecord()
        {
            WriteObject(ServiceControlCoreTransports.All.Select(PsTransportInfo.FromTransport), true);
        }
    }
}