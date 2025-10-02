namespace ServiceControl.Management.PowerShell
{
    using System.Management.Automation;

    [Cmdlet(VerbsCommon.Remove, "UrlAcl")]
    public class RemoveUrlAcl : PSCmdlet
    {
        protected override void ProcessRecord()
        {
            WriteWarning("ServiceControl no longer requires URL reservations, so this command no longer functions. Use the 'netsh http delete urlacl' command instead.");
        }
    }
}