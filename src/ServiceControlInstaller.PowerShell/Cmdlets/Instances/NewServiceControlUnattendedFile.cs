// ReSharper disable UnassignedField.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace ServiceControlInstaller.PowerShell
{
    using System;
    using System.Management.Automation;
    using ServiceControlInstaller.Engine.Instances;
    using ServiceControlInstaller.Engine.Configuration;

    [Cmdlet(VerbsCommon.New, "ServiceControlUnattendedFile")]
    public class NewServiceControlUnattendedFile : PSCmdlet
    {
        [Parameter(Mandatory = true, HelpMessage = "Specify the name of the ServiceControl Instance")]
        [ValidateNotNullOrEmpty]
        public string Name { get; set; }

        [Parameter(Mandatory = true, HelpMessage = "Specify the directory to use for this ServiceControl Instance")]
        [ValidateNotNullOrEmpty]
        [ValidatePath]
        public string InstallPath { get; set; }

        [Parameter(Mandatory = true, HelpMessage = "Specify the directory to use for this ServiceControl RavenDB")]
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

        [Parameter(Mandatory = true, HelpMessage = "Specify ErrorQueue name to consume messages from. Default is error")]
        [ValidateNotNullOrEmpty]
        public string ErrorQueue { get; set; }

        [Parameter(Mandatory = true, HelpMessage = "Specify AuditQueue name to consume messages from. Default is audit")]
        [ValidateNotNullOrEmpty]
        public string AuditQueue { get; set; }

        [Parameter(Mandatory = false, HelpMessage = "Specify Queue name to forward error messages to. Default is errorlog")]
        [ValidateNotNullOrEmpty]
        public string ErrorLogQueue { get; set; }

        [Parameter(Mandatory = false, HelpMessage = "Specify Queue name to forward audit messages to. Default is auditlog")]
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
        [ValidateNotNull]
        public bool ForwardAuditMessages { get; set; }

        [Parameter(Mandatory = false, HelpMessage = "Specify if error messages are forwarded to the queue specified by ErrorLogQueue")]
        [ValidateNotNull]
        public bool ForwardErrorMessages { get; set; }

        [Parameter(Mandatory = true, HelpMessage = "Specify the timespan to keep Audit Data")]
        [ValidateNotNull]
        [ValidateTimeSpanRange(MinimumHours = SettingConstants.AuditRetentionPeriodMinInHours, MaximumHours = SettingConstants.AuditRetentionPeriodMaxInHours)] //1 hour to 365 days
        public TimeSpan AuditRetentionPeriod {get; set;}
        
        [Parameter(Mandatory = true, HelpMessage = "Specify the timespan to keep Error Data")]
        [ValidateNotNull]
        [ValidateTimeSpanRange(MinimumHours = SettingConstants.ErrorRetentionPeriodMinInHours, MaximumHours = SettingConstants.ErrorRetentionPeriodMaxInHours)] //10 to 45 days
        public TimeSpan ErrorRetentionPeriod { get; set; }
        
        [Parameter(Mandatory = true, HelpMessage = "The path of the XML file save the output to")]
        [ValidateNotNullOrEmpty]
        [ValidatePath]
        public string OutputFile { get; set; }

        protected override void BeginProcessing()
        {
            EnsureDependentPropertyIsBoundIfSet(ForwardErrorMessages, nameof(ErrorLogQueue), "ErrorLogQueue must be specified if ForwardErrorMessages is true");
            EnsureDependentPropertyIsBoundIfSet(ForwardAuditMessages, nameof(AuditLogQueue), "AuditLogQueue must be specified if ForwardAuditMessages is true");

            HostName = HostName ?? "localhost";
        }

        void EnsureDependentPropertyIsBoundIfSet(bool enabled, string propertyName, string errormessage)
        {
            if (!MyInvocation.BoundParameters.ContainsKey(propertyName) && enabled)
            {
                throw new PSArgumentException(errormessage, propertyName);
            }
        }

        protected override void ProcessRecord()
        {
            var details = new ServiceControlInstanceMetadata
            {
                InstallPath = InstallPath,
                LogPath =  LogPath,
                DBPath = DBPath,
                Name = Name,
                DisplayName = string.IsNullOrWhiteSpace(DisplayName) ? Name : DisplayName,
                ServiceDescription = Description,
                HostName = HostName,
                Port = Port,
                VirtualDirectory = VirtualDirectory,
                AuditLogQueue = AuditLogQueue,
		        AuditQueue = AuditQueue,
		        ErrorLogQueue = ErrorLogQueue,
		        ErrorQueue = ErrorQueue,
		        ForwardAuditMessages = ForwardAuditMessages,
                ForwardErrorMessages = ForwardErrorMessages,
                ConnectionString = ConnectionString,
		        TransportPackage = Transport,
                AuditRetentionPeriod = AuditRetentionPeriod,
                ErrorRetentionPeriod = ErrorRetentionPeriod
            };
            details.Save(OutputFile);
        }
    }
}