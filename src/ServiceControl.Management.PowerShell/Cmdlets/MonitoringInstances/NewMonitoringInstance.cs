namespace ServiceControl.Management.PowerShell
{
    using System;
    using System.Management.Automation;
    using System.Threading.Tasks;
    using Cmdlets.Instances;
    using ServiceControlInstaller.Engine.Instances;
    using ServiceControlInstaller.Engine.Unattended;
    using ServiceControlInstaller.Engine.Validation;
    using Validation;

    using PathInfo = ServiceControlInstaller.Engine.Validation.PathInfo;

    [Cmdlet(VerbsCommon.New, "MonitoringInstance")]
    public class NewMonitoringInstance : PSCmdlet
    {
        [Parameter(Mandatory = true, HelpMessage = "Specify the name of the Monitoring Instance")]
        [ValidateNotNullOrEmpty]
        public string Name { get; set; }

        [Parameter(Mandatory = true, HelpMessage = "Specify the directory to use for this Monitoring Instance")]
        [ValidateNotNullOrEmpty]
        [ValidatePath]
        public string InstallPath { get; set; }

        [Parameter(Mandatory = true, HelpMessage = "Specify the directory to use for logging")]
        [ValidateNotNullOrEmpty]
        [ValidatePath]
        public string LogPath { get; set; }

        [Parameter(Mandatory = false, HelpMessage = "Specify the hostname to use in the URLACL (defaults to localhost)")]
        [ValidateNotNullOrEmpty]
        public string HostName { get; set; }

        [Parameter(Mandatory = true, HelpMessage = "Specify the port number to listen on")]
        [ValidateRange(1, 49151)]
        public int Port { get; set; }

        [Parameter(Mandatory = false, HelpMessage = "Specify ErrorQueue name to consume messages from. Default is error")]
        [ValidateNotNullOrEmpty]
        public string ErrorQueue { get; set; }

        [Parameter(Mandatory = true, HelpMessage = "Specify the NServiceBus Transport to use")]
        [ValidateSet(typeof(TransportValuesGenerator))]
        public string Transport { get; set; }

        [Parameter(Mandatory = false, HelpMessage = "Specify the Windows Service Display name. If unspecified the instance name will be used")]
        [ValidateNotNullOrEmpty]
        public string DisplayName { get; set; }

        [Parameter(Mandatory = false, HelpMessage = "Specify the connection string to use to connect to the queuing system.  Can be left blank for MSMQ")]
        [ValidateNotNullOrEmpty]
        public string ConnectionString { get; set; }

        [Parameter(Mandatory = false, HelpMessage = "Specify the description to use on the Windows Service for this instance")]
        [ValidateNotNullOrEmpty]
        public string Description { get; set; }

        [Parameter(HelpMessage = "The Account to run the Windows service. If not specified then LocalSystem is used")]
        public string ServiceAccount { get; set; }

        [Parameter(HelpMessage = "The password for the ServiceAccount.  Do not use for builtin accounts or managed serviceaccount")]
        public string ServiceAccountPassword { get; set; }

        [Parameter(Mandatory = false, HelpMessage = "Do not automatically create queues")]
        public SwitchParameter SkipQueueCreation { get; set; }

        [Parameter(Mandatory = false)]
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
        }

        protected override void ProcessRecord()
        {
            var monitoringNewInstance = new MonitoringNewInstance
            {
                InstallPath = InstallPath,
                LogPath = LogPath,
                Name = Name,
                InstanceName = Name,
                DisplayName = string.IsNullOrWhiteSpace(DisplayName) ? Name : DisplayName,
                ServiceDescription = Description,
                ServiceAccount = ServiceAccount,
                ServiceAccountPwd = ServiceAccountPassword,
                HostName = HostName,
                Port = Port,
                ErrorQueue = ErrorQueue,
                ConnectionString = ConnectionString,
                TransportPackage = ServiceControlCoreTransports.Find(Transport),
                SkipQueueCreation = SkipQueueCreation
            };
            var details = monitoringNewInstance;

            var logger = new PSLogger(Host);

            var installer = new UnattendMonitoringInstaller(logger);

            var checks = new PowerShellCommandChecks(this, Acknowledgements);
            if (!checks.CanAddInstance().GetAwaiter().GetResult())
            {
                return;
            }
            if (!checks.ValidateNewInstance(monitoringNewInstance).GetAwaiter().GetResult())
            {
                return;
            }

            try
            {
                logger.Info("Installing Monitoring instance...");
                var result = installer.Add(details, PromptToProceed);
                result.Wait();
                if (result.Result)
                {
                    var instance = InstanceFinder.FindMonitoringInstance(details.Name);
                    if (instance != null)
                    {
                        WriteObject(PsMonitoringService.FromInstance(instance));
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
                throw new EngineValidationException($"The directory specified for {pathInfo.Name} is not empty. Use -Force to if you are sure you want to use this path");
            }

            WriteWarning($"The directory specified for {pathInfo.Name} is not empty but will be used as -Force was specified");
            return Task.FromResult(false);
        }
    }
}