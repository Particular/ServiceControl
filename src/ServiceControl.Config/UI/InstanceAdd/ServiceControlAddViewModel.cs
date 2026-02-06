namespace ServiceControl.Config.UI.InstanceAdd
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.ServiceProcess;
    using System.Windows.Input;
    using PropertyChanged;
    using ServiceControl.Config.Extensions;
    using Validar;
    using Xaml.Controls;

    [InjectValidation]
    public class ServiceControlAddViewModel : ServiceControlEditorViewModel
    {
        public ServiceControlAddViewModel()
        {
            DisplayName = "ADD SERVICECONTROL";
            GetWindowsServiceNames = () => ServiceController.GetServices().Select(windowsService => windowsService.ServiceName).ToArray();
            ConventionName = "Particular.ServiceControl";
            OnConventionNameChanged();

            var i = 0;
            while (ConventionName != ErrorInstanceName)
            {
                ConventionName = $"Particular.ServiceControl.{++i}";
                // ErrorInstanceName updated via OnConventionNameChanged added by Fody
            }

            ServiceControl.PropertyChanged += ServiceControl_PropertyChanged;
            ServiceControlAudit.PropertyChanged += ServiceControl_PropertyChanged;
        }

        void ServiceControl_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (sender == ServiceControl)
            {
                NotifyOfPropertyChange($"Error{e.PropertyName}");
            }

            if (sender == ServiceControlAudit)
            {
                NotifyOfPropertyChange($"Audit{e.PropertyName}");
            }
        }

        public Func<string[]> GetWindowsServiceNames { get; set; }

        public string ConventionName { get; set; }

        public void OnConventionNameChanged()
        {
            ApplyConventionalServiceNameToErrorInstance(ConventionName);

            ApplyConventionalServiceNameToAuditInstance(ConventionName);
        }

        public override void OnSelectedTransportChanged()
        {
            base.OnSelectedTransportChanged();
            ServiceControl?.SelectedTransportChanged();
            ServiceControlAudit?.SelectedTransportChanged();
            NotifyOfPropertyChange(nameof(ConnectionString));
        }

        public string ErrorInstanceName
        {
            get => ServiceControl.InstanceName;
            set => ServiceControl.InstanceName = value.SanitizeInstanceName();
        }

        protected void OnErrorInstanceNameChanged()
        {
            ErrorDestinationPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "Particular Software", ErrorInstanceName);
            ErrorDatabasePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "Particular", "ServiceControl", ErrorInstanceName, "DB");
            ErrorLogPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "Particular", "ServiceControl", ErrorInstanceName, "Logs");
        }

        [AlsoNotifyFor(nameof(ErrorPasswordEnabled),
            nameof(ErrorServiceAccount),
            nameof(ErrorPassword))]
        public bool ErrorUseSystemAccount
        {
            get => ServiceControl.UseSystemAccount;
            set => ServiceControl.UseSystemAccount = value;
        }

        [AlsoNotifyFor(nameof(ErrorPasswordEnabled),
            nameof(ErrorServiceAccount),
            nameof(ErrorPassword))]
        public bool ErrorUseServiceAccount
        {
            get => ServiceControl.UseServiceAccount;
            set => ServiceControl.UseServiceAccount = value;

        }

        [AlsoNotifyFor(nameof(ErrorPasswordEnabled),
            nameof(ErrorServiceAccount),
            nameof(ErrorPassword))]
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

        [AlsoNotifyFor(nameof(ErrorHostNameWarning))]
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
            set => ServiceControl.LogPath = value.SanitizeFilePath();
        }

        public ICommand ErrorSelectDatabasePath => ServiceControl.SelectDatabasePath;

        public string ErrorDatabasePath
        {
            get => ServiceControl.DatabasePath;
            set => ServiceControl.DatabasePath = value.SanitizeFilePath();
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

        [AlsoNotifyFor(nameof(ErrorForwardingQueueName), nameof(ErrorForwardingWarning))]
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

        [AlsoNotifyFor(nameof(ErrorForwarding))]
        public string ErrorForwardingWarning => ServiceControl.ErrorForwardingWarning;

        public IEnumerable<EnableFullTextSearchOnBodiesOption> ErrorEnableFullTextSearchOnBodiesOptions =>
            ServiceControl.EnableFullTextSearchOnBodiesOptions;

        public EnableFullTextSearchOnBodiesOption ErrorEnableFullTextSearchOnBodies
        {
            get => ServiceControl.EnableFullTextSearchOnBodies;
            set => ServiceControl.EnableFullTextSearchOnBodies = value;
        }

        public IEnumerable<EnableIntegratedServicePulseOption> ErrorEnableIntegratedServicePulseOptions =>
            ServiceControl.EnableIntegratedServicePulseOptions;

        public EnableIntegratedServicePulseOption ErrorEnableIntegratedServicePulse
        {
            get => ServiceControl.EnableIntegratedServicePulse;
            set => ServiceControl.EnableIntegratedServicePulse = value;
        }

        /* Add Audit Instance */

        public string AuditInstanceName
        {
            get => ServiceControlAudit.InstanceName;
            set => ServiceControlAudit.InstanceName = value.SanitizeInstanceName();
        }

        protected void OnAuditInstanceNameChanged()
        {
            AuditDestinationPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "Particular Software", AuditInstanceName);
            AuditDatabasePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "Particular", "ServiceControl", AuditInstanceName, "DB");
            AuditLogPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "Particular", "ServiceControl", AuditInstanceName, "Logs");
        }

        [AlsoNotifyFor(nameof(AuditPasswordEnabled),
            nameof(AuditServiceAccount),
            nameof(ErrorPassword))]
        public bool AuditUseSystemAccount
        {
            get => ServiceControlAudit.UseSystemAccount;
            set => ServiceControlAudit.UseSystemAccount = value;
        }

        [AlsoNotifyFor(nameof(AuditPasswordEnabled),
            nameof(AuditServiceAccount),
            nameof(ErrorPassword))]
        public bool AuditUseServiceAccount
        {
            get => ServiceControlAudit.UseServiceAccount;
            set => ServiceControlAudit.UseServiceAccount = value;
        }


        [AlsoNotifyFor(nameof(AuditPasswordEnabled),
            nameof(AuditServiceAccount),
            nameof(ErrorPassword))]
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

        public bool AuditManagedAccount => ServiceControlAudit.ManagedAccount;

        [AlsoNotifyFor(nameof(AuditHostNameWarning))]
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
            set => ServiceControlAudit.DestinationPath = value.SanitizeFilePath();
        }

        public ICommand AuditSelectLogPath => ServiceControlAudit.SelectLogPath;

        public string AuditLogPath
        {
            get => ServiceControlAudit.LogPath;
            set => ServiceControlAudit.LogPath = value.SanitizeFilePath();
        }

        public ICommand AuditSelectDatabasePath => ServiceControlAudit.SelectDatabasePath;

        public string AuditDatabasePath
        {
            get => ServiceControlAudit.DatabasePath;
            set => ServiceControlAudit.DatabasePath = value.SanitizeFilePath();
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

        [AlsoNotifyFor(nameof(AuditForwardingQueueName), nameof(AuditForwardingWarning))]
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

        public string AuditForwardingWarning => ServiceControlAudit.AuditForwardingWarning;

        public IEnumerable<EnableFullTextSearchOnBodiesOption> AuditEnableFullTextSearchOnBodiesOptions =>
            ServiceControlAudit.EnableFullTextSearchOnBodiesOptions;

        public EnableFullTextSearchOnBodiesOption AuditEnableFullTextSearchOnBodies
        {
            get => ServiceControlAudit.EnableFullTextSearchOnBodies;
            set => ServiceControlAudit.EnableFullTextSearchOnBodies = value;
        }

        public bool SubmitAttempted { get; set; }

        string GetConventionalServiceName(string conventionName)
        {
            var instanceName = string.Empty;

            var validServiceName = conventionName.SanitizeInstanceName();

            if (!conventionName.StartsWith("Particular.", StringComparison.InvariantCultureIgnoreCase))
            {
                instanceName += "Particular.";
            }

            instanceName += string.IsNullOrEmpty(conventionName) ? "ServiceControl" : validServiceName;

            var windowsServiceNames = GetWindowsServiceNames();

            instanceName = CreateUniqueInstanceName(instanceName, windowsServiceNames);

            return instanceName;
        }
        public void ApplyConventionalServiceNameToErrorInstance(string conventionName) =>
            ErrorInstanceName = GetConventionalServiceName(conventionName);

        public void ApplyConventionalServiceNameToAuditInstance(string conventionName)
        {
            var uniqueServiceName = GetConventionalServiceName(conventionName);

            if (!uniqueServiceName.EndsWith(".Audit", StringComparison.InvariantCultureIgnoreCase))
            {
                uniqueServiceName += ".Audit";
            }

            AuditInstanceName = uniqueServiceName;
        }

        public string CreateUniqueInstanceName(string instanceName, string[] windowsServiceNames)
        {
            int i = 1;

            while (windowsServiceNames.Any(p => instanceName.Equals(p, StringComparison.InvariantCultureIgnoreCase)))
            {
                instanceName = $"{instanceName}-{i++}";
            }

            return instanceName;
        }
    }
}