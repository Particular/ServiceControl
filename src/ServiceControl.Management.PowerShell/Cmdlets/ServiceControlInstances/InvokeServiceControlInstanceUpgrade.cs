namespace ServiceControl.Management.PowerShell
{
    using System;
    using System.Linq;
    using System.Management.Automation;
    using System.Threading.Tasks;
    using ServiceControl.Engine.Extensions;
    using ServiceControlInstaller.Engine.Configuration.ServiceControl;
    using ServiceControlInstaller.Engine.Instances;
    using ServiceControlInstaller.Engine.Unattended;
    using ServiceControlInstaller.Engine.Validation;
    using Validation;

    using PathInfo = ServiceControlInstaller.Engine.Validation.PathInfo;

    [Cmdlet(VerbsLifecycle.Invoke, "ServiceControlInstanceUpgrade")]
    public class InvokeServiceControlInstanceUpgrade : PSCmdlet
    {
        [ValidateNotNullOrEmpty]
        [Parameter(Mandatory = true, Position = 0, HelpMessage = "Specify the name of the ServiceControl Instance to update")]
        public string Name;

        [Parameter(Mandatory = false, HelpMessage = "Specify the directory to use for the new ServiceControl Audit Instance")]
        [ValidatePath]
        public string InstallPath { get; set; }

        [Parameter(Mandatory = false, HelpMessage = "Specify the directory that will contain the RavenDB database for the new ServiceControl Audit Instance")]
        [ValidatePath]
        public string DBPath { get; set; }

        [Parameter(Mandatory = false, HelpMessage = "Specify the directory to use for the new ServiceControl Audit Logs")]
        [ValidatePath]
        public string LogPath { get; set; }

        [Parameter(Mandatory = false, HelpMessage = "Specify the port number for the new ServiceControl Audit API to listen on")]
        [ValidateRange(1, 49151)]
        public int? Port { get; set; }

        [Parameter(Mandatory = false, HelpMessage = "Specify the database maintenance port number for the new ServiceControl Audit instance to listen on")]
        [ValidateRange(1, 49151)]
        public int? DatabaseMaintenancePort { get; set; }

        [Parameter(Mandatory = false, HelpMessage = "Service Account Password (if required)")]
        public string ServiceAccountPassword { get; set; }

        [Parameter(Mandatory = false, HelpMessage = "Do not automatically create new queues")]
        public SwitchParameter SkipQueueCreation { get; set; }

        [Parameter(Mandatory = false, HelpMessage = "Disable full text search on error messages.")]
        public SwitchParameter DisableFullTextSearchOnBodies { get; set; }

        [Parameter(Mandatory = false, HelpMessage = "Reuse the specified log, db, and install paths even if they are not empty")]
        public SwitchParameter Force { get; set; }

        [Parameter(Mandatory = false, HelpMessage = "Acknowledge mandatory requirements have been met.")]
        public string[] Acknowledgements { get; set; }

        protected override void BeginProcessing()
        {
            AppDomain.CurrentDomain.AssemblyResolve += BindingRedirectAssemblyLoader.CurrentDomain_BindingRedirect;

            Account.TestIfAdmin();
        }

        protected override void ProcessRecord()
        {
            var logger = new PSLogger(Host);

            var installer = new UnattendServiceControlInstaller(logger);

            var instance = InstanceFinder.FindInstanceByName<ServiceControlInstance>(Name);
            if (instance == null)
            {
                WriteWarning($"No action taken. An instance called {Name} was not found");
                return;
            }

            var requiredUpgradeAction = instance.GetRequiredUpgradeAction(installer.ZipInfo.Version);

            switch (requiredUpgradeAction)
            {
                case RequiredUpgradeAction.Upgrade:
                    PerformUpgrade(instance, installer);
                    break;
                case RequiredUpgradeAction.SplitOutAudit:
                    PerformSplit(instance, logger).Wait();
                    break;
                case RequiredUpgradeAction.ConvertToAudit:
                default:
                    ThrowTerminatingError(new ErrorRecord(new Exception($"Upgrade of {instance.Name} aborted. This instance cannot be upgraded."), "UpgradeFailure", ErrorCategory.InvalidResult, null));
                    break;
            }
        }

        async Task PerformSplit(ServiceControlInstance instance, PSLogger logger)
        {
            AssertValidForAuditSplit(instance.Name);

            var serviceControlSplitter = new UnattendServiceControlSplitter(logger);

            var options = new UnattendServiceControlSplitter.Options
            {
                InstallPath = InstallPath,
                DBPath = DBPath,
                LogPath = LogPath,
                Port = Port.GetValueOrDefault(),
                DatabaseMaintenancePort = DatabaseMaintenancePort.GetValueOrDefault(),
                ServiceAccountPassword = ServiceAccountPassword,
                DisableFullTextSearchOnBodies = DisableFullTextSearchOnBodies
            };

            var result = await serviceControlSplitter.Split(instance, options, PromptToProceed).ConfigureAwait(false);

            WriteObject(result.Succeeded);

            if (!result.Succeeded)
            {
                var errorMessage = $"Upgrade of {instance.Name} aborted. {result.FailureReason}.";

                ThrowTerminatingError(new ErrorRecord(new Exception(errorMessage), "UpgradeFailure", ErrorCategory.InvalidResult, null));
            }
        }

        void AssertValidForAuditSplit(string instanceName)
        {
            AssertNotEmptyForAuditInstance(instanceName, InstallPath, nameof(InstallPath));
            AssertNotEmptyForAuditInstance(instanceName, DBPath, nameof(DBPath));
            AssertNotEmptyForAuditInstance(instanceName, LogPath, nameof(LogPath));
            AssertNotNullForAuditInstance(instanceName, Port, nameof(Port));
            AssertNotNullForAuditInstance(instanceName, DatabaseMaintenancePort, nameof(DatabaseMaintenancePort));
            // ServiceAccountPassword can be null. If this is a problem, it will be caught later
        }

        void AssertNotEmptyForAuditInstance(string instanceName, string paramValue, string paramName)
        {
            if (string.IsNullOrWhiteSpace(paramValue))
            {
                ThrowTerminatingError(new ErrorRecord(new Exception($"Upgrade of {instanceName} aborted. {paramName} parameter must be set to create ServiceControl Audit instance."), "UpgradeFailure", ErrorCategory.InvalidArgument, null));
            }
        }

        void AssertNotNullForAuditInstance(string instanceName, int? paramValue, string paramName)
        {
            if (!paramValue.HasValue)
            {
                ThrowTerminatingError(new ErrorRecord(new Exception($"Upgrade of {instanceName} aborted. {paramName} parameter must be set to create ServiceControl Audit instance."), "UpgradeFailure", ErrorCategory.InvalidArgument, null));
            }
        }

        void PerformUpgrade(ServiceControlInstance instance, UnattendServiceControlInstaller installer)
        {
            var options = new ServiceControlUpgradeOptions
            {
                SkipQueueCreation = SkipQueueCreation,
                DisableFullTextSearchOnBodies = DisableFullTextSearchOnBodies,
                UpgradeInfo =
                    UpgradeControl.GetUpgradeInfoForTargetVersion(installer.ZipInfo.Version, instance.Version),
            };

            if (instance.TransportPackage.IsOldRabbitMQTransport() &&
               (Acknowledgements == null || !Acknowledgements.Any(ack => ack.Equals(AcknowledgementValues.RabbitMQBrokerVersion310, StringComparison.OrdinalIgnoreCase))))
            {
                ThrowTerminatingError(new ErrorRecord(new Exception($"ServiceControl version {installer.ZipInfo.Version} requires RabbitMQ broker version 3.10.0 or higher. Also, the stream_queue and quorum_queue feature flags must be enabled on the broker. Use -Acknowledgements {AcknowledgementValues.RabbitMQBrokerVersion310} if you are sure your broker meets these requirements."), "Install Error", ErrorCategory.InvalidArgument, null));
            }

            if (!installer.Upgrade(instance, options))
            {
                ThrowTerminatingError(new ErrorRecord(new Exception($"Upgrade of {instance.Name} failed"), "UpgradeFailure", ErrorCategory.InvalidResult, null));
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