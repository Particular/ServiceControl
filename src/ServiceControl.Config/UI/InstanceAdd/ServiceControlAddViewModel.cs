namespace ServiceControl.Config.UI.InstanceAdd
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Windows.Input;
    using PropertyChanged;
    using ServiceControlInstaller.Engine.Instances;
    using Validar;
    using Xaml.Controls;

    [InjectValidation]
    public class ServiceControlAddViewModel : ServiceControlEditorViewModel
    {
        public ServiceControlAddViewModel()
        {
            DisplayName = "ADD SERVICECONTROL";
        }

        public string ConventionName { get; set; }

        public void OnConventionNameChanged()
        {
            ApplyConventionalServiceNameToErrorInstance(ConventionName);

            ApplyConventionalServiceNameToAuditInstance(ConventionName);
        }

        public virtual void OnSelectedTransportChanged()
        {
            ServiceControl?.SelectedTransportChanged();
            ServiceControlAudit?.SelectedTransportChanged();
            NotifyOfPropertyChange(nameof(ConnectionString));
        }

        public string ErrorInstanceName
        {
            get => ServiceControl.InstanceName;
            set => ServiceControl.InstanceName = value;
        }

        protected void OnErrorInstanceNameChanged()
        {
            ErrorDestinationPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Particular Software", ErrorInstanceName);
            ErrorDatabasePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Particular", "ServiceControl", ErrorInstanceName, "DB");
            ErrorLogPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Particular", "ServiceControl", ErrorInstanceName, "Logs");
        }

        public bool ErrorUseSystemAccount
        {
            get => ServiceControl.UseSystemAccount;
            set => ServiceControl.UseSystemAccount = value;
        }

        public bool ErrorUseServiceAccount
        {
            get => ServiceControl.UseServiceAccount;
            set => ServiceControl.UseServiceAccount = value;
        }

        public bool ErrorUseProvidedAccount
        {
            get => ServiceControl.UseProvidedAccount;
            set => ServiceControl.UseProvidedAccount = value;
        }

        public string ErrorServiceAccount
        {
            get => ServiceControl.ServiceAccount;
            set => ServiceControl.ServiceAccount = value;
        }

        public string ErrorPassword
        {
            get => ServiceControl.Password;
            set => ServiceControl.Password = value;
        }

        public bool ErrorPasswordEnabled => ServiceControl.PasswordEnabled;

        public bool ErrorManagedAccount => ServiceControl.ManagedAccount;

        public string ErrorHostName
        {
            get => ServiceControl.HostName;
            set => ServiceControl.HostName = value;
        }

        public string ErrorHostNameWarning
        {
            get => ServiceControl.HostNameWarning;
            set => ServiceControl.HostNameWarning = value;
        }

        public string ErrorPortNumber
        {
            get => ServiceControl.PortNumber;
            set => ServiceControl.PortNumber = value;
        }

        public string ErrorDatabaseMaintenancePortNumber
        {
            get => ServiceControl.DatabaseMaintenancePortNumber;
            set => ServiceControl.DatabaseMaintenancePortNumber = value;
        }

        public ICommand ErrorSelectDestinationPath => ServiceControl.SelectDestinationPath;


        public string ErrorDestinationPath
        {
            get => ServiceControl.DestinationPath;
            set => ServiceControl.DestinationPath = value;
        }

        public ICommand ErrorSelectLogPath => ServiceControl.SelectLogPath;

        public string ErrorLogPath
        {
            get => ServiceControl.LogPath;
            set => ServiceControl.LogPath = value;
        }

        public ICommand ErrorSelectDatabasePath => ServiceControl.SelectDatabasePath;

        public string ErrorDatabasePath
        {
            get => ServiceControl.DatabasePath;
            set => ServiceControl.DatabasePath = value;
        }

        public int MinimumErrorRetentionPeriod => ServiceControl.MinimumErrorRetentionPeriod;

        public int MaximumErrorRetentionPeriod => ServiceControl.MaximumErrorRetentionPeriod;

        public TimeSpanUnits ErrorRetentionUnits => ServiceControl.ErrorRetentionUnits;

        public double ErrorRetention
        {
            get => ServiceControl.ErrorRetention;
            set => ServiceControl.ErrorRetention = value;
        }

        public string ErrorQueueName
        {
            get => ServiceControl.ErrorQueueName;
            set => ServiceControl.ErrorQueueName = value;
        }

        public IEnumerable<ForwardingOption> ErrorForwardingOptions => ServiceControl.ErrorForwardingOptions;

        public ForwardingOption ErrorForwarding
        {
            get => ServiceControl.ErrorForwarding;
            set => ServiceControl.ErrorForwarding = value;
        }

        public string ErrorForwardingQueueName
        {
            get => ServiceControl.ErrorForwardingQueueName;
            set => ServiceControl.ErrorForwardingQueueName = value;
        }

        public bool ShowErrorForwardingQueue => ErrorForwarding?.Value ?? false;

        [AlsoNotifyFor("ErrorForwarding")]
        public string ErrorForwardingWarning => ServiceControl.ErrorForwardingWarning;


        public IEnumerable<EnableFullTextSearchOnBodiesOption> ErrorEnableFullTextSearchOnBodiesOptions =>
            ServiceControl.EnableFullTextSearchOnBodiesOptions;

        public EnableFullTextSearchOnBodiesOption ErrorEnableFullTextSearchOnBodies
        {
            get => ServiceControl.EnableFullTextSearchOnBodies;
            set => ServiceControl.EnableFullTextSearchOnBodies = value;
        }

        /* Add Audit Instance */

        public string AuditInstanceName
        {
            get => ServiceControlAudit.InstanceName;
            set => ServiceControlAudit.InstanceName = value;
        }

        protected void OnAuditInstanceNameChanged()
        {
            AuditDestinationPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Particular Software", AuditInstanceName);
            AuditDatabasePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Particular", "ServiceControl", AuditInstanceName, "DB");
            AuditLogPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Particular", "ServiceControl", AuditInstanceName, "Logs");
        }

        public bool AuditUseSystemAccount
        {
            get => ServiceControlAudit.UseSystemAccount;
            set => ServiceControlAudit.UseSystemAccount = value;
        }

        public bool AuditUseServiceAccount
        {
            get => ServiceControlAudit.UseServiceAccount;
            set => ServiceControlAudit.UseServiceAccount = value;
        }

        public bool AuditUseProvidedAccount
        {
            get => ServiceControlAudit.UseProvidedAccount;
            set => ServiceControlAudit.UseProvidedAccount = value;
        }

        public string AuditServiceAccount
        {
            get => ServiceControlAudit.ServiceAccount;
            set => ServiceControlAudit.ServiceAccount = value;
        }

        public string AuditPassword
        {
            get => ServiceControlAudit.Password;
            set => ServiceControlAudit.Password = value;
        }

        public bool AuditPasswordEnabled => ServiceControlAudit.PasswordEnabled;

        public bool AuditManaged => ServiceControlAudit.ManagedAccount;

        public string AuditHostName
        {
            get => ServiceControlAudit.HostName;
            set => ServiceControlAudit.HostName = value;
        }

        public string AuditHostNameWarning
        {
            get => ServiceControlAudit.HostNameWarning;
            set => ServiceControlAudit.HostNameWarning = value;
        }

        public string AuditPortNumber
        {
            get => ServiceControlAudit.PortNumber;
            set => ServiceControlAudit.PortNumber = value;
        }

        public string AuditDatabaseMaintenancePortNumber
        {
            get => ServiceControlAudit.DatabaseMaintenancePortNumber;
            set => ServiceControlAudit.DatabaseMaintenancePortNumber = value;
        }

        public ICommand AuditSelectDestinationPath => ServiceControlAudit.SelectDestinationPath;

        public string AuditDestinationPath
        {
            get => ServiceControlAudit.DestinationPath;
            set => ServiceControlAudit.DestinationPath = value;
        }

        public ICommand AuditSelectLogPath => ServiceControlAudit.SelectLogPath;

        public string AuditLogPath
        {
            get => ServiceControlAudit.LogPath;
            set => ServiceControlAudit.LogPath = value;
        }

        public ICommand AuditSelectDatabasePath => ServiceControlAudit.SelectDatabasePath;

        public string AuditDatabasePath
        {
            get => ServiceControlAudit.DatabasePath;
            set => ServiceControlAudit.DatabasePath = value;
        }

        public int MaximumAuditRetentionPeriod => ServiceControlAudit.MaximumAuditRetentionPeriod;
        public int MinimumAuditRetentionPeriod => ServiceControlAudit.MinimumAuditRetentionPeriod;

        public TimeSpanUnits AuditRetentionUnits => ServiceControlAudit.AuditRetentionUnits;

        public bool IsServiceControlExpanded { get; set; }

        public bool IsServiceControlAuditExpanded { get; set; }

        public double AuditRetention
        {
            get => ServiceControlAudit.AuditRetention;
            set => ServiceControlAudit.AuditRetention = value;
        }

        public string AuditQueueName
        {
            get => ServiceControlAudit.AuditQueueName;
            set => ServiceControlAudit.AuditQueueName = value;
        }

        public IEnumerable<ForwardingOption> AuditForwardingOptions => ServiceControlAudit.AuditForwardingOptions;

        public ForwardingOption AuditForwarding
        {
            get => ServiceControlAudit.AuditForwarding;
            set => ServiceControlAudit.AuditForwarding = value;
        }

        public string AuditForwardingQueueName
        {
            get => ServiceControlAudit.AuditForwardingQueueName;
            set => ServiceControlAudit.AuditForwardingQueueName = value;
        }

        public bool ShowAuditForwardingQueue => AuditForwarding?.Value ?? false;

        [AlsoNotifyFor("AuditForwarding")]
        public string AuditForwardingWarning => ServiceControlAudit.AuditForwardingWarning;

        public IEnumerable<EnableFullTextSearchOnBodiesOption> AudiEnableFullTextSearchOnBodiesOptions => ServiceControlAudit.EnableFullTextSearchOnBodiesOptions;

        public EnableFullTextSearchOnBodiesOption AuditEnableFullTextSearchOnBodies
        {
            get => ServiceControlAudit.EnableFullTextSearchOnBodies;
            set => ServiceControlAudit.EnableFullTextSearchOnBodies = value;
        }

        public bool SubmitAttempted { get; set; }

        int GetInstalledInstancesCount()
        {
            var serviceControlInstances = InstanceFinder.ServiceControlInstances();
            if (!serviceControlInstances.Any())
            {
                return 0;
            }

            var i = 0;
            while (true)
            {
                i++;
                if (!serviceControlInstances.Any(p => p.Name.Equals(ErrorInstanceName, StringComparison.OrdinalIgnoreCase)))
                {
                    return i;
                }
            }
        }

        string RemoveIllegalCharacters(string name)
        {
            return name?.Replace(' ', '.');
        }

        string GetConventionalServiceName(string suggestedName)
        {
            var instanceName = string.Empty;
            var instanceCount = GetInstalledInstancesCount();
            var titleCaseName = CultureInfo.CurrentUICulture.TextInfo.ToTitleCase(suggestedName);
            var serviceBaseName = instanceCount == 0 ? "ServiceControl" : "ServiceControl-" + instanceCount;

            if (!suggestedName.StartsWith("Particular.", StringComparison.InvariantCultureIgnoreCase))
            {
                instanceName += "Particular.";
            }

            instanceName += !string.IsNullOrEmpty(suggestedName) ? titleCaseName : serviceBaseName;

            return RemoveIllegalCharacters(instanceName);
        }
        public void ApplyConventionalServiceNameToErrorInstance(string conventionName) => ErrorInstanceName = GetConventionalServiceName(conventionName);

        public void ApplyConventionalServiceNameToAuditInstance(string conventionName)
        {
            var serviceName = GetConventionalServiceName(conventionName);

            if (!serviceName.EndsWith(".Audit", StringComparison.InvariantCultureIgnoreCase))
            {
                serviceName += ".Audit";
            }

            AuditInstanceName = serviceName;
        }
    }
}