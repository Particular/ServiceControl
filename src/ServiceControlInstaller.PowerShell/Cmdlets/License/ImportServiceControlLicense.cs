// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace ServiceControlInstaller.PowerShell
{
    using System;
    using System.Management.Automation;
    using Engine.LicenseMgmt;
    using Microsoft.PowerShell.Commands;

    [Cmdlet(VerbsData.Import, "ServiceControlLicense")]
    public class ImportServiceControlLicense : PSCmdlet
    {
        [ValidateNotNullOrEmpty]
        [Alias("FullName")]
        [Parameter(Mandatory = true, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, Position = 0, HelpMessage = "Specify the path ot the license file")]
        public string File { get; set; }

        protected override void BeginProcessing()
        {
            Account.TestIfAdmin();
        }

        protected override void ProcessRecord()
        {
            var psPath = SessionState.Path.GetUnresolvedProviderPathFromPSPath(File, out var provider, out _);

            if (provider.ImplementingType != typeof(FileSystemProvider))
            {
                var ex = new ArgumentException($"{psPath} does not resolve to a path on the FileSystem provider.");
                var error = new ErrorRecord(ex, "InvalidProvider", ErrorCategory.InvalidArgument, psPath);
                WriteError(error);
                return;
            }

            string errorMsg;
            if (!LicenseManager.TryImportLicense(psPath, out errorMsg))
            {
                WriteError(new ErrorRecord(new Exception(errorMsg), "ServiceControlLicense", ErrorCategory.InvalidData, null));
            }
        }
    }
}