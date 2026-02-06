namespace ServiceControl.Management.PowerShell
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Management.Automation;
    using System.Threading.Tasks;
    using ServiceControlInstaller.Engine.Instances;
    using ServiceControlInstaller.Engine.Unattended;
    using ServiceControlInstaller.Engine.Validation;
    using Validation;

    using PathInfo = ServiceControlInstaller.Engine.Validation.PathInfo;

    [Cmdlet(VerbsCommon.New, "ServiceControlInstance")]
    public class NewServiceControlInstance : PSCmdlet
    {
        [Parameter(Mandatory = true, HelpMessage = "Specify the name of the ServiceControl Instance")]
        [ValidateNotNullOrEmpty]
        public string Name { get; set; }

        [Parameter(Mandatory = true, HelpMessage = "Specify the directory to use for this ServiceControl Instance")]
        [ValidateNotNullOrEmpty]
        [ValidatePath]
        public string InstallPath { get; set; }

        [Parameter(Mandatory = true, HelpMessage = "Specify the directory that will contain the RavenDB database for this ServiceControl Instance")]
        [ValidateNotNullOrEmpty]
        [ValidatePath]
        public string DBPath { get; set; }

        [Parameter(Mandatory = true, HelpMessage = "Specify the directory to use for this ServiceControl Logs")]
        [ValidateNotNullOrEmpty]
        [ValidatePath]
        public string LogPath { get; set; }

        [Parameter(Mandatory = false, HelpMessage = "Specify the hostname to use in the URLACL (defaults to localhost)")]
        [ValidateNotNullOrEmpty]
        public string HostName { get; set; }

        [Parameter(Mandatory = true, HelpMessage = "Specify the port number to listen on. If this is the only ServiceControl instance then 33333 is recommended")]
        [ValidateRange(1, 49151)]
        public int Port { get; set; }

        [Parameter(Mandatory = true, HelpMessage = "Specify the database maintenance port number to listen on. If this is the only ServiceControl instance then 33334 is recommended")]
        [ValidateRange(1, 49151)]
        public int DatabaseMaintenancePort { get; set; }

        [Parameter(Mandatory = false, HelpMessage = "Specify ErrorQueue name to consume messages from. Default is error")]
        [ValidateNotNullOrEmpty]
        public string ErrorQueue { get; set; }

        [Parameter(Mandatory = false, HelpMessage = "Specify Queue name to forward error messages to")]
        [ValidateNotNullOrEmpty]
        public string ErrorLogQueue { get; set; }

        [Parameter(Mandatory = true, HelpMessage = "Specify the NServiceBus Transport to use")]
        [ValidateSet(typeof(TransportValuesGenerator))]
        public string Transport { get; set; }

        [Parameter(Mandatory = false, HelpMessage = "Specify the Windows Service Display name. If unspecified the instance name will be used")]
        [ValidateNotNullOrEmpty]
        public string DisplayName { get; set; }

        [Parameter(Mandatory = false, HelpMessage = "Specify an alternate VirtualDirectory to use. This option is not recommended")]
        [ValidateNotNullOrEmpty]
        public string VirtualDirectory { get; set; }

        [Parameter(Mandatory = false, HelpMessage = "Specify the connection string to use to connect to the queuing system.  Can be left blank for MSMQ")]
        [ValidateNotNullOrEmpty]
        public string ConnectionString { get; set; }

        [Parameter(Mandatory = false, HelpMessage = "Specify the description to use on the Windows Service for this instance")]
        [ValidateNotNullOrEmpty]
        public string Description { get; set; }

        [Parameter(Mandatory = false, HelpMessage = "Specify if error messages are forwarded to the queue specified by ErrorLogQueue")]
        public SwitchParameter ForwardErrorMessages { get; set; }

        [Parameter(HelpMessage = "The Account to run the Windows service. If not specified then LocalSystem is used")]
        public string ServiceAccount { get; set; }

        [Parameter(HelpMessage = "The password for the ServiceAccount.  Do not use for builtin accounts or managed serviceaccount")]
        public string ServiceAccountPassword { get; set; }

        [Parameter(Mandatory = true, HelpMessage = "Specify the timespan to keep Error Data")]
        [ValidateNotNull]
        [ValidateTimeSpanRange(MinimumHours = 120, MaximumHours = 1080)] //5 to 45 days
        public TimeSpan ErrorRetentionPeriod { get; set; }

        [Parameter(Mandatory = false, HelpMessage = "Do not automatically create queues")]
        public SwitchParameter SkipQueueCreation { get; set; }

        [Parameter(Mandatory = false, HelpMessage = "Specify whether to enable full text search on error messages.")]
        public SwitchParameter EnableFullTextSearchOnBodies { get; set; } = true;

        [Parameter(Mandatory = false, HelpMessage = "Specify whether to enable integrated ServicePulse instance.")]
        public SwitchParameter EnableIntegratedServicePulse { get; set; }

        [Parameter(Mandatory = false, HelpMessage = "Reuse the specified log, db, and install paths even if they are not empty")]
        public SwitchParameter Force { get; set; }

        [Parameter(Mandatory = false, HelpMessage = "Acknowledge mandatory requirements have been met.")]
        public string[] Acknowledgements { get; set; }

        protected override void BeginProcessing()
        {
            var transport = ServiceControlCoreTransports.Find(Transport);

            var requiresConnectionString = !string.IsNullOrEmpty(transport.SampleConnectionString);
            var hasConnectionString = !string.IsNullOrEmpty(ConnectionString);

            if (requiresConnectionString && !hasConnectionString)
            {
                throw new Exception($"ConnectionString is mandatory for '{Transport}'");
            }

            if (!requiresConnectionString && hasConnectionString)
            {
                throw new Exception($"'{Transport}' does not use a connection string.");
            }

            if (!transport.AvailableInSCMU)
            {
                WriteWarning($"The transport '{Transport}' is deprecated. Consult the corresponding upgrade guide for the selected transport on 'https://docs.particular.net'");
            }

            if (string.IsNullOrWhiteSpace(HostName))
            {
                WriteWarning("HostName set to default value 'localhost'");
                HostName = "localhost";
            }

            if (string.IsNullOrWhiteSpace(ErrorQueue))
            {
                WriteWarning("ErrorQueue set to default value 'error'");
                ErrorQueue = "error";
            }

            if (string.IsNullOrWhiteSpace(ServiceAccount))
            {
                WriteWarning("ServiceAccount set to default value 'LocalSystem'");
                ServiceAccount = "LocalSystem";
            }

            if (ForwardErrorMessages.ToBool() & string.IsNullOrWhiteSpace(ErrorLogQueue))
            {
                WriteWarning("ErrorLogQueue set to default value 'error.log'");
                ErrorLogQueue = "error.log";
            }
        }

        protected override void ProcessRecord()
        {
            var details = ServiceControlNewInstance.CreateWithDefaultPersistence();

            details.InstallPath = InstallPath;
            details.LogPath = LogPath;
            details.DBPath = DBPath;
            details.Name = Name;
            details.InstanceName = Name;
            details.DisplayName = string.IsNullOrWhiteSpace(DisplayName) ? Name : DisplayName;
            details.ServiceDescription = Description;
            details.ServiceAccount = ServiceAccount;
            details.ServiceAccountPwd = ServiceAccountPassword;
            details.HostName = HostName;
            details.Port = Port;
            details.DatabaseMaintenancePort = DatabaseMaintenancePort;
            details.VirtualDirectory = VirtualDirectory;
            details.ErrorQueue = ErrorQueue;
            details.ErrorLogQueue = string.IsNullOrWhiteSpace(ErrorLogQueue) ? null : ErrorLogQueue;
            details.ForwardErrorMessages = ForwardErrorMessages.ToBool();
            details.ErrorRetentionPeriod = ErrorRetentionPeriod;
            details.ConnectionString = ConnectionString;
            details.TransportPackage = ServiceControlCoreTransports.Find(Transport);
            details.SkipQueueCreation = SkipQueueCreation;
            details.EnableFullTextSearchOnBodies = EnableFullTextSearchOnBodies;
            details.EnableIntegratedServicePulse = EnableIntegratedServicePulse;

            var modulePath = Path.GetDirectoryName(MyInvocation.MyCommand.Module.Path);

            var logger = new PSLogger(Host);
            var installer = new UnattendServiceControlInstaller(logger);
            try
            {
                var checks = new PowerShellCommandChecks(this, Acknowledgements);
                if (!checks.CanAddInstance().GetAwaiter().GetResult())
                {
                    return;
                }
                if (!checks.ValidateNewInstance(details).GetAwaiter().GetResult())
                {
                    return;
                }

                logger.Info("Module root at " + modulePath);
                logger.Info("Installing Service Control instance...");
                var result = installer.Add(details, PromptToProceed);
                result.Wait();
                if (result.Result)
                {
                    var instance = InstanceFinder.FindInstanceByName<ServiceControlInstance>(details.Name);
                    if (instance != null)
                    {
                        WriteObject(PsServiceControl.FromInstance(instance));
                    }
                    else
                    {
                        throw new Exception("Unknown error creating instance");
                    }
                }
                else
                {
                    var msg = "Installer did not run successfully.";

                    if (details.ReportCard?.HasErrors == true)
                    {
                        var errors = details.ReportCard.Errors.Select(e => e);
                        msg += string.Join(Environment.NewLine, errors);
                    }

                    throw new Exception(msg);
                }
            }
            catch (Exception ex)
            {
                ThrowTerminatingError(new ErrorRecord(ex, null, ErrorCategory.NotSpecified, null));
            }
        }

        Task<bool> PromptToProceed(PathInfo pathInfo)
        {
            if (!pathInfo.CheckIfEmpty)
            {
                return Task.FromResult(false);
            }

            if (!Force.ToBool())
            {
                throw new EngineValidationException($"The directory specified for {pathInfo.Name} is not empty.  Use -Force if you are sure you want to use this path");
            }

            WriteWarning($"The directory specified for {pathInfo.Name} is not empty but will be used as -Force was specified");
            return Task.FromResult(false);
        }
    }
}
