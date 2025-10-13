namespace ServiceControl.Management.PowerShell
{
    using System;
    using System.Management.Automation;
    using Particular.Obsoletes;

    [ObsoleteMetadata(Message = "ServiceControl no longer requires URL reservations, so this command no longer functions", ReplacementTypeOrMember = "netsh http show urlacl", TreatAsErrorFromVersion = "7", RemoveInVersion = "8")]
    [Obsolete("ServiceControl no longer requires URL reservations, so this command no longer functions. Use 'netsh http show urlacl' instead. Will be treated as an error from version 7.0.0. Will be removed in version 8.0.0.", false)]
    [Cmdlet(VerbsCommon.Get, "UrlAcls")]
    public class GetUrlAcls : PSCmdlet;
}