namespace ServiceControl.Management.PowerShell
{
    using System.Management.Automation;

    [Cmdlet(VerbsCommon.Get, "UrlAcls")]
    public class GetUrlAcls : PSCmdlet
    {
        protected override void ProcessRecord()
        {
            WriteWarning("ServiceControl no longer requires URL reservations, so this command no longer functions. Use the 'netsh http show urlacl' command instead.");
        }
    }
}