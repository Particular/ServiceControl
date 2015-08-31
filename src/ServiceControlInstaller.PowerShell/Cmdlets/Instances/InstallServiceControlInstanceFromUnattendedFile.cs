// ReSharper disable UnassignedField.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace ServiceControlInstaller.PowerShell
{
    using System;
    using System.IO;
    using System.Management.Automation;
    using Microsoft.PowerShell.Commands;
    using ServiceControlInstaller.Engine.Instances;
    using ServiceControlInstaller.Engine.Unattended;

    [Cmdlet(VerbsCommon.New, "ServiceControlInstanceFromUnattendedFile")]
    public class NewServiceControlInstanceFromUnattendedFile : PSCmdlet
    {
        [ValidateNotNullOrEmpty]
        [Alias("FullName")]
        [Parameter(Mandatory = true, ValueFromPipeline = true,ValueFromPipelineByPropertyName= true, Position = 0, HelpMessage = "Specify the path to the XML file")]
        public string UnattendFile { get; set; }

        [ValidateNotNullOrEmpty]
        [Parameter(Mandatory = false, Position = 1, HelpMessage = "Specify the ServiceAccount to use")]
        public string ServiceAccount { get; set; }

        [Parameter(Mandatory = false, Position = 2, HelpMessage = "Specify the ServiceAccount Password")]
        public string Password { get; set; }

        protected override void BeginProcessing()
        {
            Account.TestIfAdmin();
        }

        protected override void ProcessRecord()
        {
            ProviderInfo provider;
            PSDriveInfo drive;
            var psPath =  SessionState.Path.GetUnresolvedProviderPathFromPSPath(UnattendFile, out provider, out drive);

            if (provider.ImplementingType != typeof(FileSystemProvider))
            {
                var ex = new ArgumentException(string.Format("{0} does not resolve to a path on the FileSystem provider.", psPath));
                var error = new ErrorRecord(ex, "InvalidProvider", ErrorCategory.InvalidArgument, psPath);
                WriteError(error);
                return;
            }
            var details = ServiceControlInstanceMetadata.Load(psPath);
            details.ServiceAccount = ServiceAccount;
            details.ServiceAccountPwd = Password;
            var zipfolder = Path.GetDirectoryName(MyInvocation.MyCommand.Module.Path);
            var logger = new PSLogger(Host);
            var installer = new UnattendInstaller(logger, zipfolder);
            try
            {
                logger.Info("Installing Service Control instance...");
                installer.Add(details);
                WriteObject(ServiceControlInstance.FindByName(details.Name));
            }
            catch (Exception ex)
            {
                ThrowTerminatingError(new ErrorRecord(ex, null, ErrorCategory.NotSpecified, null));
            }
        }
    }
}


