#pragma warning disable CS1591
namespace ServiceControl.Management.PowerShell
{
    using System;
    using System.Management.Automation;
    using Particular.Obsoletes;

    [ObsoleteMetadata(Message = "ServiceControl no longer requires URL reservations, so this command no longer functions", ReplacementTypeOrMember = "netsh http show urlacl", TreatAsErrorFromVersion = "7", RemoveInVersion = "8")]
    [Obsolete("ServiceControl no longer requires URL reservations, so this command no longer functions. Use 'netsh http show urlacl' instead. Will be removed in version 8.0.0.", true)]
    [Cmdlet(VerbsCommon.Get, "UrlAcls")]
    public class GetUrlAcls : PSCmdlet;

    [ObsoleteMetadata(Message = "ServiceControl no longer requires URL reservations, so this command no longer functions", ReplacementTypeOrMember = "netsh http add urlacl", TreatAsErrorFromVersion = "7", RemoveInVersion = "8")]
    [Obsolete("ServiceControl no longer requires URL reservations, so this command no longer functions. Use 'netsh http add urlacl' instead. Will be removed in version 8.0.0.", true)]
    [Cmdlet(VerbsCommon.Add, "UrlAcl")]
    public class AddUrlAcl : PSCmdlet;

    [ObsoleteMetadata(Message = "ServiceControl no longer requires URL reservations, so this command no longer functions", ReplacementTypeOrMember = "netsh http delete urlacl", TreatAsErrorFromVersion = "7", RemoveInVersion = "8")]
    [Obsolete("ServiceControl no longer requires URL reservations, so this command no longer functions. Use 'netsh http delete urlacl' instead. Will be removed in version 8.0.0.", true)]
    [Cmdlet(VerbsCommon.Remove, "UrlAcl")]
    public class RemoveUrlAcl : PSCmdlet;
}