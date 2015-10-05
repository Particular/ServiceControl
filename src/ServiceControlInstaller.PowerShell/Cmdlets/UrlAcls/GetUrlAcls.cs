namespace ServiceControlInstaller.PowerShell
{
    using System.Management.Automation;
    using ServiceControlInstaller.Engine.UrlAcl;

    [Cmdlet(VerbsCommon.Get, "UrlAcls")]
    public class GetUrlAcls : PSCmdlet
    {
        protected override void ProcessRecord()
        {
            WriteObject(UrlReservation.GetAll(), true);
        }
    }
}