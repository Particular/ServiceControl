namespace ServiceControl.Management.PowerShell
{
    using System;
    using System.Management.Automation;
    using Microsoft.PowerShell.Commands;
    using ServiceControl.LicenseManagement;

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

            if (!LicenseManager.TryImportLicense(psPath, out var errorMsg))
            {
                WriteError(new ErrorRecord(new Exception(errorMsg), "ServiceControlLicense", ErrorCategory.InvalidData, null));
            }
        }
    }
}