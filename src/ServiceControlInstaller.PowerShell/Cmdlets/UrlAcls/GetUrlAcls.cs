namespace ServiceControlInstaller.PowerShell
{
    using System.Management.Automation;
    using HttpApiWrapper;

    [Cmdlet(VerbsCommon.Get, "UrlAcls")]
    public class GetUrlAcls : PSCmdlet
    {
        protected override void ProcessRecord()
        {
            WriteObject(UrlReservation.GetAll(), true);
        }
    }
}