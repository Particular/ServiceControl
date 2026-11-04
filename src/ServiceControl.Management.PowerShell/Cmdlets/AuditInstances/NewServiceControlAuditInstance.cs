namespace ServiceControl.Management.PowerShell
{
    using System;
    using System.Collections.Generic;
    using System.Management.Automation;
    using System.Threading;
    using System.Threading.Tasks;
    using ServiceControlInstaller.Engine.Instances;
    using ServiceControlInstaller.Engine.Unattended;
    using ServiceControlInstaller.Engine.Validation;
    using Validation;

    using PathInfo = ServiceControlInstaller.Engine.Validation.PathInfo;

    [Cmdlet(VerbsCommon.New, "ServiceControlAuditInstance")]
    public class NewServiceControlAuditInstance : PSCmdlet
    {
        [Parameter(Mandatory = true, HelpMessage = "Specify the name of the Audit Instance")]
        [ValidateNotNullOrEmpty]
        public string Name { get; set; }

        [Parameter(Mandatory = true, HelpMessage = "Specify the directory to use for this Audit Instance")]
        [ValidateNotNullOrEmpty]
        [ValidatePath]
        public string InstallPath { get; set; }

        [Parameter(Mandatory = false, HelpMessage = "Specify the storage type to use. Defaults to RavenDB")]
        [ValidateSet(typeof(AuditStorageTypeValuesGenerator))]
        public string StorageType { get; set; }

        [Parameter(Mandatory = false, HelpMessage = "Specify the directory that will contain the RavenDB database for this Audit Instance. Required for RavenDB")]
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

        [Parameter(Mandatory = false, HelpMessage = "Specify the Azure Blob Storage container name. Defaults to audit-bodies")]
        [ValidateNotNullOrEmpty]
        public string AzureBlobContainerName { get; set; }

        [Parameter(Mandatory = false, HelpMessage = "Specify the S3 bucket name. Required for S3 message body storage")]
        [ValidateNotNullOrEmpty]
        public string S3BucketName { get; set; }

        [Parameter(Mandatory = false, HelpMessage = "Specify the S3 key prefix. Defaults to audit-bodies/")]
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

        [Parameter(Mandatory = true, HelpMessage = "Specify the directory to use for this Audit Instance Logs")]
        [ValidateNotNullOrEmpty]
        [ValidatePath]
        public string LogPath { get; set; }

        [Parameter(Mandatory = false, HelpMessage = "Specify the hostname to use in the URLACL (defaults to localhost)")]
        [ValidateNotNullOrEmpty]
        public string HostName { get; set; }

        [Parameter(Mandatory = true, HelpMessage = "Specify the port number to listen on")]
        [ValidateRange(1, 49151)]
        public int Port { get; set; }

        [Parameter(Mandatory = false, HelpMessage = "Specify the database maintenance port number to listen on. Required for RavenDB")]
        [ValidateRange(1, 49151)]
        public int? DatabaseMaintenancePort { get; set; }

        [Parameter(Mandatory = false, HelpMessage = "Specify AuditQueue name to consume messages from. Default is audit")]
        [ValidateNotNullOrEmpty]
        public string AuditQueue { get; set; }

        [Parameter(Mandatory = false, HelpMessage = "Specify Queue name to forward audit messages to")]
        public string AuditLogQueue { get; set; }

        [Parameter(Mandatory = true, HelpMessage = "Specify the NServiceBus Transport to use")]
        [ValidateSet(typeof(TransportValuesGenerator))]
        public string Transport { get; set; }

        [Parameter(Mandatory = false, HelpMessage = "Specify the Windows Service Display name. If unspecified the instance name will be used")]
        [ValidateNotNullOrEmpty]
        public string DisplayName { get; set; }

        [Parameter(Mandatory = false, HelpMessage = "Specify the connection string to use to connect to the queuing system.  Can be left blank for MSMQ")]
        public string ConnectionString { get; set; }

        [Parameter(Mandatory = false, HelpMessage = "Specify the description to use on the Windows Service for this instance")]
        [ValidateNotNullOrEmpty]
        public string Description { get; set; }

        [Parameter(Mandatory = false, HelpMessage = "Specify if audit messages are forwarded to the queue specified by AuditLogQueue")]
        public SwitchParameter ForwardAuditMessages { get; set; }

        [Parameter(HelpMessage = "The Account to run the Windows service. If not specified then LocalSystem is used")]
        public string ServiceAccount { get; set; }

        [Parameter(HelpMessage = "The password for the ServiceAccount.  Do not use for builtin accounts or managed serviceaccount")]
        public string ServiceAccountPassword { get; set; }

        [Parameter(Mandatory = true, HelpMessage = "Specify the timespan to keep Audit Data")]
        [ValidateNotNull]
        [ValidateTimeSpanRange(MinimumHours = 1, MaximumHours = 8760)] //1 hour to 365 days
        public TimeSpan AuditRetentionPeriod { get; set; }

        [Parameter(Mandatory = true, HelpMessage = "The name of the ServiceControl instance to connect to")]
        [ValidateNotNull]
        public string ServiceControlQueueAddress { get; set; }

        [Parameter(Mandatory = false, HelpMessage = "Do not automatically create queues")]
        public SwitchParameter SkipQueueCreation { get; set; }

        [Parameter(Mandatory = false, HelpMessage = "Specify whether to enable full text search on audit message bodies. Only used when StorageType is RavenDB.")]
        public SwitchParameter EnableFullTextSearchOnBodies { get; set; } = true;

        [Parameter(Mandatory = false, HelpMessage = "Reuse the specified log, db, and install paths even if they are not empty")]
        public SwitchParameter Force { get; set; }

        [Parameter(Mandatory = false, HelpMessage = "Acknowledge mandatory requirements have been met.")]
        public string[] Acknowledgements { get; set; }

        protected override void BeginProcessing()
        {
            persistenceManifest = ServiceControlPersisters.GetAuditPersistence(string.IsNullOrWhiteSpace(StorageType) ? StorageEngineNames.RavenDB : StorageType);
            PersistenceParameters.Validate(persistenceManifest, DBPath, DatabaseMaintenancePort, DatabaseConnectionString, MyInvocation.BoundParameters.ContainsKey(nameof(EnableFullTextSearchOnBodies)));
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

            if (string.IsNullOrWhiteSpace(AuditQueue))
            {
                WriteWarning("AuditQueue set to default value 'audit'");
                AuditQueue = "audit";
            }

            if (string.IsNullOrWhiteSpace(ServiceAccount))
            {
                WriteWarning("ServiceAccount set to default value 'LocalSystem'");
                ServiceAccount = "LocalSystem";
            }
        }

        protected override void ProcessRecord()
        {
            var newAuditInstance = ServiceControlAuditNewInstance.CreateWithPersistence(persistenceManifest.Name);

            newAuditInstance.InstallPath = InstallPath;
            newAuditInstance.LogPath = LogPath;
            newAuditInstance.DBPath = DBPath;
            newAuditInstance.Name = Name;
            newAuditInstance.InstanceName = Name;
            newAuditInstance.DisplayName = string.IsNullOrWhiteSpace(DisplayName) ? Name : DisplayName;
            newAuditInstance.ServiceDescription = Description;
            newAuditInstance.ServiceAccount = ServiceAccount;
            newAuditInstance.ServiceAccountPwd = ServiceAccountPassword;
            newAuditInstance.HostName = HostName;
            newAuditInstance.Port = Port;
            newAuditInstance.DatabaseMaintenancePort = DatabaseMaintenancePort;
            newAuditInstance.PersistenceConnectionString = DatabaseConnectionString;
            newAuditInstance.MessageBodyStorage = messageBodyStorage;
            newAuditInstance.AuditQueue = AuditQueue;
            newAuditInstance.AuditLogQueue = string.IsNullOrWhiteSpace(AuditLogQueue) ? null : AuditLogQueue;
            newAuditInstance.ForwardAuditMessages = ForwardAuditMessages.ToBool();
            newAuditInstance.AuditRetentionPeriod = AuditRetentionPeriod;
            newAuditInstance.ConnectionString = ConnectionString;
            newAuditInstance.TransportPackage = ServiceControlCoreTransports.Find(Transport);
            newAuditInstance.SkipQueueCreation = SkipQueueCreation;
            newAuditInstance.ServiceControlQueueAddress = ServiceControlQueueAddress;
            newAuditInstance.EnableFullTextSearchOnBodies = EnableFullTextSearchOnBodies;

            var logger = new PSLogger(Host);

            var installer = new UnattendAuditInstaller(logger);

            var checks = new PowerShellCommandChecks(this, Acknowledgements);
            if (!checks.CanAddInstance().GetAwaiter().GetResult())
            {
                return;
            }
            if (!checks.ValidateNewInstance([newAuditInstance]).GetAwaiter().GetResult())
            {
                return;
            }

            try
            {
                logger.Info("Installing Service Control Audit instance...");
                var result = installer.Add(newAuditInstance, PromptToProceed);
                result.Wait();
                if (result.Result)
                {
                    var instance = InstanceFinder.FindInstanceByName<ServiceControlAuditInstance>(newAuditInstance.Name);
                    if (instance != null)
                    {
                        WriteObject(PsAuditInstance.FromInstance(instance));
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
