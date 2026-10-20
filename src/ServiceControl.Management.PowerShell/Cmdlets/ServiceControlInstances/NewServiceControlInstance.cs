namespace ServiceControl.Management.PowerShell
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Management.Automation;
    using System.Threading;
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

        [Parameter(Mandatory = false, HelpMessage = "Specify the persistence type to use. Defaults to RavenDB")]
        [ValidateSet(typeof(PrimaryPersistenceTypeValuesGenerator))]
        public string PersistenceType { get; set; }

        [Parameter(Mandatory = false, HelpMessage = "Specify the directory that will contain the RavenDB database for this ServiceControl Instance. Required for RavenDB")]
        [ValidateNotNullOrEmpty]
        [ValidatePath]
        public string DBPath { get; set; }

        [Parameter(Mandatory = false, HelpMessage = "Specify the database connection string. Required for SQLServer and PostgreSQL")]
        [ValidateNotNullOrEmpty]
        public string DatabaseConnectionString { get; set; }

        [Parameter(Mandatory = false, HelpMessage = "Specify where message bodies are stored for SQLServer and PostgreSQL. Defaults to FileSystem")]
        [ValidateSet("FileSystem", "AzureBlob", "S3")]
        public string MessageBodyStorageType { get; set; }

        [Parameter(Mandatory = false, HelpMessage = "Specify the directory that will contain message bodies. Required for FileSystem message body storage")]
        [ValidateNotNullOrEmpty]
        [ValidatePath]
        public string MessageBodyPath { get; set; }

        [Parameter(Mandatory = false, HelpMessage = "Specify the Azure Blob Storage connection string. Use this or AzureBlobServiceUri")]
        [ValidateNotNullOrEmpty]
        public string AzureBlobConnectionString { get; set; }

        [Parameter(Mandatory = false, HelpMessage = "Specify the Azure Blob Storage service URI to connect with a managed identity. Use this or AzureBlobConnectionString")]
        [ValidateNotNullOrEmpty]
        public string AzureBlobServiceUri { get; set; }

        [Parameter(Mandatory = false, HelpMessage = "Specify the client ID of a user-assigned managed identity")]
        [ValidateNotNullOrEmpty]
        public string AzureBlobManagedIdentityClientId { get; set; }

        [Parameter(Mandatory = false, HelpMessage = "Specify the Microsoft Entra authority host for sovereign clouds")]
        [ValidateNotNullOrEmpty]
        public string AzureBlobAuthorityHost { get; set; }

        [Parameter(Mandatory = false, HelpMessage = "Specify the Azure Blob Storage container name. Defaults to error-bodies")]
        [ValidateNotNullOrEmpty]
        public string AzureBlobContainerName { get; set; }

        [Parameter(Mandatory = false, HelpMessage = "Specify the S3 bucket name. Required for S3 message body storage")]
        [ValidateNotNullOrEmpty]
        public string S3BucketName { get; set; }

        [Parameter(Mandatory = false, HelpMessage = "Specify the S3 key prefix. Defaults to error-bodies/")]
        [ValidateNotNullOrEmpty]
        public string S3KeyPrefix { get; set; }

        [Parameter(Mandatory = false, HelpMessage = "Specify the S3 region")]
        [ValidateNotNullOrEmpty]
        public string S3Region { get; set; }

        [Parameter(Mandatory = false, HelpMessage = "Specify the service URL of S3-compatible storage")]
        [ValidateNotNullOrEmpty]
        public string S3ServiceUrl { get; set; }

        [Parameter(Mandatory = false, HelpMessage = "Specify the S3 access key ID. Use with S3SecretAccessKey, or omit both to use the default AWS credentials")]
        [ValidateNotNullOrEmpty]
        public string S3AccessKeyId { get; set; }

        [Parameter(Mandatory = false, HelpMessage = "Specify the S3 secret access key. Use with S3AccessKeyId")]
        [ValidateNotNullOrEmpty]
        public string S3SecretAccessKey { get; set; }

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

        [Parameter(Mandatory = false, HelpMessage = "Specify the database maintenance port number to listen on. If this is the only ServiceControl instance then 33334 is recommended. Required for RavenDB")]
        [ValidateRange(1, 49151)]
        public int? DatabaseMaintenancePort { get; set; }

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
            persistenceManifest = ServiceControlPersisters.GetPrimaryPersistence(string.IsNullOrWhiteSpace(PersistenceType) ? StorageEngineNames.RavenDB : PersistenceType);
            PersistenceParameters.Validate(persistenceManifest, DBPath, DatabaseMaintenancePort, DatabaseConnectionString);
            messageBodyStorage = PersistenceParameters.CreateMessageBodyStorage(persistenceManifest, MessageBodyStorageType, new Dictionary<string, string>
            {
                [nameof(MessageBodyPath)] = MessageBodyPath,
                [nameof(AzureBlobConnectionString)] = AzureBlobConnectionString,
                [nameof(AzureBlobServiceUri)] = AzureBlobServiceUri,
                [nameof(AzureBlobManagedIdentityClientId)] = AzureBlobManagedIdentityClientId,
                [nameof(AzureBlobAuthorityHost)] = AzureBlobAuthorityHost,
                [nameof(AzureBlobContainerName)] = AzureBlobContainerName,
                [nameof(S3BucketName)] = S3BucketName,
                [nameof(S3KeyPrefix)] = S3KeyPrefix,
                [nameof(S3Region)] = S3Region,
                [nameof(S3ServiceUrl)] = S3ServiceUrl,
                [nameof(S3AccessKeyId)] = S3AccessKeyId,
                [nameof(S3SecretAccessKey)] = S3SecretAccessKey
            });

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
            var details = ServiceControlNewInstance.CreateWithPersistence(persistenceManifest.Name);

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
            details.PersistenceConnectionString = DatabaseConnectionString;
            details.MessageBodyStorage = messageBodyStorage;
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
                if (!checks.CanAddInstance(true).GetAwaiter().GetResult())
                {
                    return;
                }
                if (!checks.ValidateNewInstance([details]).GetAwaiter().GetResult())
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

        PersistenceManifest persistenceManifest;
        MessageBodyStorage messageBodyStorage;

        Task<bool> PromptToProceed(PathInfo pathInfo, CancellationToken cancellationToken)
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
