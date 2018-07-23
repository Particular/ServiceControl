// ReSharper disable UnassignedField.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace ServiceControlInstaller.PowerShell
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Management.Automation;
    using Engine.Instances;
    using Engine.Unattended;
    using Engine.Validation;
    using PathInfo = Engine.Validation.PathInfo;

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

        [Parameter(Mandatory = false, HelpMessage = "Specify AuditQueue name to consume messages from. Default is audit")]
        [ValidateNotNullOrEmpty]
        public string AuditQueue { get; set; }

        [Parameter(Mandatory = false, HelpMessage = "Specify Queue name to forward error messages to")]
        [ValidateNotNullOrEmpty]
        public string ErrorLogQueue { get; set; }

        [Parameter(Mandatory = false, HelpMessage = "Specify Queue name to forward audit messages to")]
        [ValidateNotNullOrEmpty]
        public string AuditLogQueue { get; set; }

        [Parameter(Mandatory = true, HelpMessage = "Specify the NServiceBus Transport to use")]
        [ValidateSet("AzureServiceBus", "AzureStorageQueue", "MSMQ", "SQLServer", "RabbitMQ")]
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

        [Parameter(Mandatory = true, HelpMessage = "Specify if audit messages are forwarded to the queue specified by AuditLogQueue")]
        public SwitchParameter ForwardAuditMessages { get; set; }

        [Parameter(Mandatory = false, HelpMessage = "Specify if error messages are forwarded to the queue specified by ErrorLogQueue")]
        public SwitchParameter ForwardErrorMessages { get; set; }

        [Parameter(HelpMessage = "The Account to run the Windows service. If not specified then LocalSystem is used")]
        public string ServiceAccount { get; set; }

        [Parameter(HelpMessage = "The password for the ServiceAccount.  Do not use for builtin accounts or managed serviceaccount")]
        public string ServiceAccountPassword { get; set; }

        [Parameter(Mandatory = true, HelpMessage = "Specify the timespan to keep Audit Data")]
        [ValidateNotNull]
        [ValidateTimeSpanRange(MinimumHours = 1, MaximumHours = 8760)] //1 hour to 365 days
        public TimeSpan AuditRetentionPeriod { get; set; }

        [Parameter(Mandatory = true, HelpMessage = "Specify the timespan to keep Error Data")]
        [ValidateNotNull]
        [ValidateTimeSpanRange(MinimumHours = 240, MaximumHours = 1080)] //10 to 45 days
        public TimeSpan ErrorRetentionPeriod { get; set; }

        [Parameter(Mandatory = false, HelpMessage = "Do not automatically create queues")]
        public SwitchParameter SkipQueueCreation { get; set; }

        [Parameter(Mandatory = false)]
        public SwitchParameter Force { get; set; }

        protected override void BeginProcessing()
        {
            if (string.IsNullOrWhiteSpace(HostName))
            {
                WriteWarning("HostName set to default value 'localhost'");
                HostName = "localhost";
            }

            if (string.IsNullOrWhiteSpace(AuditQueue))
            {
                WriteWarning("AuditQueue set to default value 'audit'");
                AuditQueue = "audit";
            }

            if (string.IsNullOrWhiteSpace(ErrorQueue))
            {
                WriteWarning("ErrorQueue set to default value 'error'");
                ErrorQueue = "error";
            }

            if (string.IsNullOrWhiteSpace(ServiceAccount))
            {
                WriteWarning("ServiceAccountset to default value 'LocalSystem'");
                ServiceAccount = "LocalSystem";
            }

            if (ForwardAuditMessages.ToBool() & string.IsNullOrWhiteSpace(AuditLogQueue))
            {
                WriteWarning("AuditLogQueue set to default value 'audit.log'");
                ErrorLogQueue = "audit.log";
            }

            if (ForwardErrorMessages.ToBool() & string.IsNullOrWhiteSpace(ErrorLogQueue))
            {
                WriteWarning("ErrorLogQueue set to default value 'error.log'");
                ErrorLogQueue = "error.log";
            }
        }

        protected override void ProcessRecord()
        {
            var details = new ServiceControlNewInstance
            {
                InstallPath = InstallPath,
                LogPath = LogPath,
                DBPath = DBPath,
                Name = Name,
                DisplayName = string.IsNullOrWhiteSpace(DisplayName) ? Name : DisplayName,
                ServiceDescription = Description,
                ServiceAccount = ServiceAccount,
                ServiceAccountPwd = ServiceAccountPassword,
                HostName = HostName,
                Port = Port,
                DatabaseMaintenancePort = DatabaseMaintenancePort,
                VirtualDirectory = VirtualDirectory,
                AuditQueue = AuditQueue,
                ErrorQueue = ErrorQueue,
                AuditLogQueue = string.IsNullOrWhiteSpace(AuditLogQueue) ? null : AuditLogQueue,
                ErrorLogQueue = string.IsNullOrWhiteSpace(ErrorLogQueue) ? null : ErrorLogQueue,
                ForwardAuditMessages = ForwardAuditMessages.ToBool(),
                ForwardErrorMessages = ForwardErrorMessages.ToBool(),
                AuditRetentionPeriod = AuditRetentionPeriod,
                ErrorRetentionPeriod = ErrorRetentionPeriod,
                ConnectionString = ConnectionString,
                TransportPackage = ServiceControlCoreTransports.All.First(t => t.Matches(Transport)),
                SkipQueueCreation = SkipQueueCreation
            };

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
            if (!pathInfo.CheckIfEmpty)
            {
                return false;
            }

            if (!Force.ToBool())
            {
                throw new EngineValidationException($"The directory specified for {pathInfo.Name} is not empty.  Use -Force to if you are sure you want to use this path");
            }

            WriteWarning($"The directory specified for {pathInfo.Name} is not empty but will be used as -Force was specified");
            return false;
        }
    }
}