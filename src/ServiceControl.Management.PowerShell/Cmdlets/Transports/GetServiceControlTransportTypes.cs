namespace ServiceControl.Management.PowerShell
{
    using System.Management.Automation;
    using ServiceControlInstaller.Engine.Instances;

    [Cmdlet(VerbsCommon.Get, "ServiceControlTransportTypes")]
    public class GetServiceControlTransportTypes : Cmdlet
    {
        protected override void ProcessRecord()
        {
            WriteObject(ServiceControlCoreTransports.Select(PsTransportInfo.FromTransport), true);
        }
    }
}