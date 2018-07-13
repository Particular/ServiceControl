
// ReSharper disable UnassignedField.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace ServiceControlInstaller.PowerShell
{
    using System;
    using System.IO;
    using System.Management.Automation;
    using ServiceControlInstaller.Engine.Instances;
    using ServiceControlInstaller.Engine.Unattended;
    using ServiceControlInstaller.Engine.Validation;
    using PathInfo = Engine.Validation.PathInfo;

    [Cmdlet(VerbsCommon.New, "ServiceControlInstanceFromUnattendedFile")]
    public class NewServiceControlInstanceFromUnattendedFile : PSCmdlet
    {
        [ValidateNotNullOrEmpty]
        [Alias("FullName")]
        [ValidatePath]
        [Parameter(Mandatory = true, ValueFromPipeline = true,ValueFromPipelineByPropertyName= true, Position = 0, HelpMessage = "Specify the path to the XML file")]
        public string UnattendFile { get; set; }

        [ValidateNotNullOrEmpty]
        [Parameter(Mandatory = false, Position = 1, HelpMessage = "Specify the ServiceAccount to use")]
        public string ServiceAccount { get; set; }

        [Parameter(Mandatory = false, Position = 2, HelpMessage = "Specify the ServiceAccount Password")]
        public string Password { get; set; }

        [Parameter(Mandatory = false)]
        public SwitchParameter Force { get; set; }

        protected override void BeginProcessing()
        {
            WriteWarning("New-ServiceControlInstanceFromUnattendedFile is deprecated. Please New-ServiceControlInstance via PSScript it automate installation");
            Account.TestIfAdmin();
        }

        protected override void ProcessRecord()
        {
            var psPath =  SessionState.Path.GetUnresolvedProviderPathFromPSPath(UnattendFile, out _, out _);
            
            var details = ServiceControlNewInstance.Load(psPath);
            details.ServiceAccount = ServiceAccount;
            details.ServiceAccountPwd = Password;
            var zipfolder = Path.GetDirectoryName(MyInvocation.MyCommand.Module.Path);
            var logger = new PSLogger(Host);
            var installer = new UnattendServiceControlInstaller(logger, zipfolder);
            try
            {
                logger.Info("Installing Service Control instance...");
                if (installer.Add(details, PromptToProceed))
                {
                    var instance = InstanceFinder.FindServiceControlInstance(details.Name);
                    if (instance != null)
                    {
                        WriteObject(PsServiceControl.FromInstance(instance));
                    }
                    else
                    {
                        throw new Exception("Unknown error creating instance");
                    }
                }
            }
            catch (Exception ex)
            {
                ThrowTerminatingError(new ErrorRecord(ex, null, ErrorCategory.NotSpecified, null));
            }
        }

        private bool PromptToProceed(PathInfo pathInfo)
        {
            if (!pathInfo.CheckIfEmpty) return false;
            if (!Force.ToBool())
                throw new EngineValidationException($"Thr directory specified for {pathInfo.Name} is not empty.  Use -Force to if you are sure you want to use this path");
            WriteWarning($"Thr directory specified for {pathInfo.Name} is not empty but will be used as -Force was specified");
            return false;
        }
    }
}


