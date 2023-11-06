namespace ServiceControl.Management.PowerShell
{
    using System.Linq;
    using System.Management.Automation;
    using ServiceControlInstaller.Engine.Instances;

    [Cmdlet(VerbsCommon.Get, "ServiceControlTransportTypes")]
    public class GetServiceControlTransportTypes : Cmdlet
    {
        protected override void ProcessRecord()
        {
            // Perhaps in the future only show deprecated transports if extra parameter is given
            bool allTransports = true;

            var transportInfos = ServiceControlCoreTransports.GetPowerShellTransports(allTransports)
                .Select(PsTransportInfo.FromTransport);

            WriteObject(transportInfos, true);
        }
    }
}