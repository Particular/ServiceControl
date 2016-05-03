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
    
    [Cmdlet(VerbsCommon.New, "ServiceControlInstance")]
    public class NewServiceControlInstance : PSCmdlet
    {
        [ValidateNotNullOrEmpty]
        [Parameter(Mandatory = true, HelpMessage = "Specify the name of the ServiceControl Instance")]
        public string Name { get; set; }

        [ValidateNotNullOrEmpty]
        [ValidatePath]
        [Parameter(Mandatory = true, HelpMessage = "Specify the directory to use for this ServiceControl Instance")]
        public string InstallPath { get; set; }

        [ValidateNotNullOrEmpty]
        [ValidatePath]
        [Parameter(Mandatory = true, HelpMessage = "Specify the directory that will contain the RavenDB database for this ServiceControl Instance")]
        public string DBPath { get; set; }

        [ValidateNotNullOrEmpty]
        [ValidatePath]
        [Parameter(Mandatory = true, HelpMessage = "Specify the directory to use for this ServiceControl Logs")]
        public string LogPath { get; set; }

        [ValidateNotNullOrEmpty]
        [Parameter(Mandatory = false, HelpMessage = "Specify the hostname to use in the URLACL (defaults to localhost)")]
        public string HostName { get; set; }

        [ValidateRange(1, 49151)]
        [Parameter(Mandatory = true, HelpMessage = "Specify the port number to listen on. If this is the only ServiceControl instance then 33333 is recommended")]
        public int Port { get; set; }

        [ValidateNotNullOrEmpty]
        [Parameter(Mandatory = false, HelpMessage = "Specify ErrorQueue name to consume messages from. Default is error")]
        public string ErrorQueue { get; set; }

        [ValidateNotNullOrEmpty]
        [Parameter(Mandatory = false, HelpMessage = "Specify AuditQueue name to consume messages from. Default is audit")]
        public string AuditQueue { get; set; }
        
        [ValidateNotNullOrEmpty]
        [Parameter(Mandatory = false, HelpMessage = "Specify Queue name to forward error messages to. Default is errorlog")]
        public string ErrorLogQueue { get; set; }

        [ValidateNotNullOrEmpty]
        [Parameter(Mandatory = false, HelpMessage = "Specify Queue name to forward audit messages to. Default is auditlog")]
        public string AuditLogQueue { get; set; }

        [ValidateSet("AzureServiceBus", "AzureStorageQueue", "MSMQ", "SQLServer", "RabbitMQ")]
        [Parameter(Mandatory = true, HelpMessage = "Specify the NServiceBus Transport to use")]
        public string Transport { get; set; }

        [ValidateNotNullOrEmpty]
        [Parameter(Mandatory = false, HelpMessage = "Specify the Windows Service Display name. If unspecified the instance name will be used")]
        public string DisplayName { get; set; }

        [ValidateNotNullOrEmpty]
        [Parameter(Mandatory = false, HelpMessage = "Specify an alternate VirtualDirectory to use. This option is not recommended")]
        public string VirtualDirectory { get; set; }

        [ValidateNotNullOrEmpty]
        [Parameter(Mandatory = false, HelpMessage = "Specify the connection string to use to connect to the queuing system.  Can be left blank for MSMQ")]
        public string ConnectionString { get; set; }

        [ValidateNotNullOrEmpty]
        [Parameter(Mandatory = false, HelpMessage = "Specify the description to use on the Windows Service for this instance")]
        public string Description { get; set; }

        [Parameter(Mandatory = true, HelpMessage = "Specify if audit messages are forwarded to the queue specified by AuditLogQueue")]
        public bool ForwardAuditMessages { get; set; }
        
        [Parameter(Mandatory = true, HelpMessage = "Specify if error messages are forwarded to the queue specified by ErrorLogQueue")]
        public bool ForwardErrorMessages { get; set; }
        
        [Parameter(HelpMessage = "The Account to run the Windows service. If no specified LocalSystem is used")]
        public string ServiceAccount { get; set; }

        [Parameter(HelpMessage = "The password for the ServiceAccount.  Do not use for LocalSystem or NetworkService")]
        public string ServiceAccountPassword { get; set; }

        [Parameter(Mandatory = true, HelpMessage = "Specify the timespan to keep Audit Data")]
        [ValidateNotNull]
        [ValidateTimeSpanRange(MinimumHours = 1, MaximumHours = 8760)] //1 hour to 365 days
        public TimeSpan AuditRetentionPeriod { get; set; }

        [Parameter(Mandatory = true, HelpMessage = "Specify the timespan to keep Error Data")]
        [ValidateNotNull]
        [ValidateTimeSpanRange(MinimumHours = 240, MaximumHours = 1080)] //10 to 45 days
        public TimeSpan ErrorRetentionPeriod { get; set; }

        protected override void BeginProcessing()
        {
            //Set default values
            HostName = HostName ?? "localhost";
            ErrorQueue = ErrorQueue ?? "error";
            AuditQueue = AuditQueue ?? "audit";
            ServiceAccount = ServiceAccount ?? "LocalSystem";
        }

        protected override void ProcessRecord()
        {
            var details = new ServiceControlInstanceMetadata
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
                VirtualDirectory = VirtualDirectory,
                AuditQueue = AuditQueue,
                ErrorQueue = ErrorQueue,
                AuditLogQueue = string.IsNullOrWhiteSpace(AuditLogQueue) ? null : AuditLogQueue,
                ErrorLogQueue = string.IsNullOrWhiteSpace(ErrorLogQueue) ? null : ErrorLogQueue,
                ForwardAuditMessages = ForwardAuditMessages,
                ForwardErrorMessages = ForwardErrorMessages,
                AuditRetentionPeriod = AuditRetentionPeriod,
                ErrorRetentionPeriod = ErrorRetentionPeriod,
                ConnectionString = ConnectionString,
                TransportPackage = Transport
            };
            
            var zipfolder = Path.GetDirectoryName(MyInvocation.MyCommand.Module.Path);
            var logger = new PSLogger(Host);

            var installer = new UnattendInstaller(logger, zipfolder);
            try
            {
                logger.Info("Installing Service Control instance...");
                if (installer.Add(details))
                {
                    var instance = ServiceControlInstance.FindByName(details.Name);
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
    }
}