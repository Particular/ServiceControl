// ReSharper disable UnassignedField.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace ServiceControlInstaller.PowerShell
{
    using System;
    using System.Management.Automation;
    using Microsoft.PowerShell.Commands;
    using ServiceControlInstaller.Engine.Instances;

    [Cmdlet(VerbsCommon.New, "ServiceControlUnattendedFile")]
    public class NewServiceControlUnattendedFile : PSCmdlet
    {
        [ValidateNotNullOrEmpty]
        [Parameter(Mandatory = true, HelpMessage = "Specify the name of the ServiceControl Instance")]
        public string Name { get; set; }

        [ValidateNotNullOrEmpty]
        [Parameter(Mandatory = true, HelpMessage = "Specify the directory to use for this ServiceControl Instance")]
        public string InstallPath { get; set; }

        [ValidateNotNullOrEmpty]
        [Parameter(Mandatory = true, HelpMessage = "Specify the directory to use for this ServiceControl RavenDB")]
        public string DBPath { get; set; }

        [ValidateNotNullOrEmpty]
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

        [Parameter(HelpMessage = "Specify if audit messages are forwarded to the queue specified by AuditLogQueue")]
        public SwitchParameter ForwardAuditMessages { get; set; }

        [Parameter(HelpMessage = "Specify if error messages are forwarded to the queue specified by ErrorLogQueue")]
        public SwitchParameter ForwardErrorMessages { get; set; }


        [ValidateNotNullOrEmpty]
        [Parameter(Mandatory = true, HelpMessage = "The path of the XML file save the output to")]
        public string OutputFile { get; set; }

        protected override void BeginProcessing()
        {
            //Set default values
            HostName = HostName ?? "localhost";
            ErrorQueue = ErrorQueue ?? "error";
            AuditQueue = AuditQueue ?? "audit";
            ErrorLogQueue = ErrorLogQueue ?? "errorlog";
            AuditLogQueue = AuditLogQueue ?? "auditlog";
            
            ProviderInfo provider;
            PSDriveInfo drive;

            InstallPath = SessionState.Path.GetUnresolvedProviderPathFromPSPath(InstallPath, out provider, out drive);
            if (provider.ImplementingType != typeof(FileSystemProvider))
            {
                WriteObject(new ErrorRecord(new ArgumentException("InstallPath is invalid"), "InvalidProvider", ErrorCategory.InvalidArgument, InstallPath));
                StopProcessing();
            }

            LogPath = SessionState.Path.GetUnresolvedProviderPathFromPSPath(LogPath, out provider, out drive);
            if (provider.ImplementingType != typeof(FileSystemProvider))
            {
                WriteObject(new ErrorRecord(new ArgumentException("LogPath is invalid"), "InvalidProvider", ErrorCategory.InvalidArgument, LogPath));
                StopProcessing();
            }

            DBPath = SessionState.Path.GetUnresolvedProviderPathFromPSPath(DBPath, out provider, out drive);
            if (provider.ImplementingType != typeof(FileSystemProvider))
            {
                WriteObject(new ErrorRecord(new ArgumentException("DBPath is invalid"), "InvalidProvider", ErrorCategory.InvalidArgument, DBPath));
                StopProcessing();
            }

            OutputFile = SessionState.Path.GetUnresolvedProviderPathFromPSPath(OutputFile, out provider, out drive);
            if (provider.ImplementingType != typeof(FileSystemProvider))
            {
                WriteObject(new ErrorRecord(new ArgumentException("OutputFile is invalid"), "InvalidProvider", ErrorCategory.InvalidArgument, OutputFile));
                StopProcessing();
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
                HostName = HostName,
                Port = Port,
                VirtualDirectory = VirtualDirectory,
                AuditLogQueue = AuditLogQueue,
		        AuditQueue = AuditQueue,
		        ErrorLogQueue = ErrorLogQueue,
		        ErrorQueue = ErrorQueue,
		        ForwardAuditMessages = ForwardAuditMessages.ToBool(),
                ForwardErrorMessages = ForwardErrorMessages.ToBool(),
                ConnectionString = ConnectionString,
		        TransportPackage = Transport
            };
            details.Save(OutputFile);
        }
    }
}