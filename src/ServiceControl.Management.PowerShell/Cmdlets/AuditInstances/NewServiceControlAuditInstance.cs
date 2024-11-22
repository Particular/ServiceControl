namespace ServiceControl.Management.PowerShell
{
    using System;
    using System.Management.Automation;
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

        [Parameter(Mandatory = true, HelpMessage = "Specify the directory that will contain the RavenDB database for this Audit Instance")]
        [ValidateNotNullOrEmpty]
        [ValidatePath]
        public string DBPath { get; set; }

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

        [Parameter(Mandatory = true, HelpMessage = "Specify the database maintenance port number to listen on")]
        [ValidateRange(1, 49151)]
        public int DatabaseMaintenancePort { get; set; }

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

        [Parameter(Mandatory = false, HelpMessage = "Specify whether to enable full text search on audit messages.")]
        public SwitchParameter EnableFullTextSearchOnBodies { get; set; } = true;

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
            var newAuditInstance = ServiceControlAuditNewInstance.CreateWithDefaultPersistence();

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
            if (!checks.ValidateNewInstance(newAuditInstance).GetAwaiter().GetResult())
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