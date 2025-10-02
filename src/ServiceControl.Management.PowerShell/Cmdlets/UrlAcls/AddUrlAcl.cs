namespace ServiceControl.Management.PowerShell
{
    using System.Management.Automation;

    [Cmdlet(VerbsCommon.Add, "UrlAcl")]
    public class AddUrlAcl : PSCmdlet
    {
        [ValidateNotNullOrEmpty]
        [Parameter(Mandatory = true, Position = 0, HelpMessage = "The URL to add to the URLACL list. This should always in a trailing /")]
        public string Url { get; set; }

        protected override void ProcessRecord()
        {
            WriteWarning("ServiceControl no longer requires URL reservations, so this command no longer functions. Use the 'netsh http add urlacl' command instead.");
        }

        [ValidateNotNullOrEmpty]
        [Parameter(Mandatory = true, Position = 1, HelpMessage = "The user or group to assign to this URLACL")]
        public string[] Users;
    }
}