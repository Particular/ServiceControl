namespace ServiceControl.Management.PowerShell
{
    using System;
    using System.Management.Automation;
    using Particular.Obsoletes;

    [ObsoleteMetadata(Message = "ServiceControl no longer requires URL reservations, so this command no longer functions", ReplacementTypeOrMember = "netsh http add urlacl", TreatAsErrorFromVersion = "7", RemoveInVersion = "8")]
    [Obsolete("ServiceControl no longer requires URL reservations, so this command no longer functions. Use 'netsh http add urlacl' instead. Will be treated as an error from version 7.0.0. Will be removed in version 8.0.0.", false)]
    [Cmdlet(VerbsCommon.Add, "UrlAcl")]
    public class AddUrlAcl : PSCmdlet
    {
        [ValidateNotNullOrEmpty]
        [Parameter(Mandatory = true, Position = 0, HelpMessage = "The URL to add to the URLACL list. This should always in a trailing /")]
        public string Url { get; set; }

        [ValidateNotNullOrEmpty]
        [Parameter(Mandatory = true, Position = 1, HelpMessage = "The user or group to assign to this URLACL")]
        public string[] Users;
    }
}