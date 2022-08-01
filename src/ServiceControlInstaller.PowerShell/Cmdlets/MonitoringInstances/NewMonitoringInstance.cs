namespace ServiceControlInstaller.PowerShell
{
    using System;
    using System.Linq;
    using System.Management.Automation;
    using System.Threading.Tasks;
    using Cmdlets.Instances;
    using Engine.Instances;
    using Engine.Unattended;
    using Engine.Validation;
    using PathInfo = Engine.Validation.PathInfo;

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
        [ValidateSet(TransportNames.AzureServiceBusForwardingTopologyDeprecated, TransportNames.AzureServiceBusForwardingTopologyLegacy, TransportNames.AzureServiceBusForwardingTopologyOld, TransportNames.AzureServiceBusEndpointOrientedTopologyDeprecated, TransportNames.AzureServiceBusEndpointOrientedTopologyLegacy, TransportNames.AzureServiceBusEndpointOrientedTopologyOld, TransportNames.AzureServiceBus, TransportNames.AzureStorageQueue, TransportNames.MSMQ, TransportNames.SQLServer, TransportNames.RabbitMQClassicDirectRoutingTopology, TransportNames.RabbitMQQuorumDirectRoutingTopology, TransportNames.RabbitMQClassicConventionalRoutingTopology, TransportNames.RabbitMQQuorumConventionalRoutingTopology, TransportNames.AmazonSQS)]
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

        protected override void BeginProcessing()
        {
            AppDomain.CurrentDomain.AssemblyResolve += BindingRedirectAssemblyLoader.CurrentDomain_BindingRedirect;

            if (Transport != TransportNames.MSMQ && string.IsNullOrEmpty(ConnectionString))
            {
                throw new Exception($"ConnectionString is mandatory for '{Transport}'");
            }

            if (TransportNames.IsDeprecated(Transport))
            {
                WriteWarning($"The transport '{Transport.Replace(TransportNames.DeprecatedPrefix, string.Empty)}' is deprecated. Consult the corresponding upgrade guide for the selected transport on 'https://docs.particular.net'");
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
            var details = new MonitoringNewInstance
            {
                InstallPath = InstallPath,
                LogPath = LogPath,
                Name = Name,
                DisplayName = string.IsNullOrWhiteSpace(DisplayName) ? Name : DisplayName,
                ServiceDescription = Description,
                ServiceAccount = ServiceAccount,
                ServiceAccountPwd = ServiceAccountPassword,
                HostName = HostName,
                Port = Port,
                ErrorQueue = ErrorQueue,
                ConnectionString = ConnectionString,
                TransportPackage = ServiceControlCoreTransports.All.First(t => t.Matches(Transport)),
                SkipQueueCreation = SkipQueueCreation
            };

            var zipfolder = ZipPath.Get(this);
            var logger = new PSLogger(Host);

            var installer = new UnattendMonitoringInstaller(logger, zipfolder);
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
                throw new EngineValidationException($"The directory specified for {pathInfo.Name} is not empty.  Use -Force to if you are sure you want to use this path");
            }

            WriteWarning($"The directory specified for {pathInfo.Name} is not empty but will be used as -Force was specified");
            return Task.FromResult(false);
        }
    }
}
