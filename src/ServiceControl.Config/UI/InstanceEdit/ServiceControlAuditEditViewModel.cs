namespace ServiceControl.Config.UI.InstanceEdit
{
    using System;
    using System.Collections.Generic;
    using System.Windows.Input;
    using Extensions;
    using InstanceAdd;
    using PropertyChanged;
    using ServiceControlInstaller.Engine.Instances;
    using Validar;
    using Xaml.Controls;

    [InjectValidation]
    public class ServiceControlAuditEditViewModel : ServiceControlEditorViewModel
    {
        public ServiceControlAuditEditViewModel(ServiceControlAuditInstance instance)
        {
            DisplayName = "EDIT SERVICECONTROL AUDIT INSTANCE";
            ServiceControlInstance = instance;
            ServiceControlAudit.UpdateFromInstance(instance);
            SelectedTransport = instance.TransportPackage;
            ConnectionString = instance.ConnectionString;
        }

        //This is only used for unit testing
        internal ServiceControlAuditEditViewModel()
        {
        }

        public ServiceControlAuditInstance ServiceControlInstance { get; set; }

        public void UpdateInstanceFromViewModel(ServiceControlAuditInstance instance)
        {
            instance.HostName = ServiceControlAudit.HostName;
            instance.Port = Convert.ToInt32(ServiceControlAudit.PortNumber);
            instance.LogPath = ServiceControlAudit.LogPath;
            instance.AuditLogQueue = ServiceControlAudit.AuditForwardingQueueName;
            instance.AuditQueue = ServiceControlAudit.AuditQueueName;
            instance.AuditRetentionPeriod = ServiceControlAudit.AuditRetentionPeriod;
            instance.ForwardAuditMessages = ServiceControlAudit.AuditForwarding.Value;
            instance.ConnectionString = ConnectionString;
            instance.DatabaseMaintenancePort = Convert.ToInt32(ServiceControlAudit.DatabaseMaintenancePortNumber);
            instance.EnableFullTextSearchOnBodies = ServiceControlAudit.EnableFullTextSearchOnBodies.Value;
        }

        public string InstanceName
        {
            get => ServiceControlAudit.InstanceName;
            set => ServiceControlAudit.InstanceName = value;
        }

        [AlsoNotifyFor(
            nameof(PasswordEnabled),
            nameof(ServiceAccount),
            nameof(Password))]
        public bool UseSystemAccount
        {
            get => ServiceControlAudit.UseSystemAccount;
            set => ServiceControlAudit.UseSystemAccount = value;
        }

        [AlsoNotifyFor(nameof(PasswordEnabled),
            nameof(ServiceAccount),
            nameof(Password))]
        public bool UseServiceAccount
        {
            get => ServiceControlAudit.UseServiceAccount;
            set => ServiceControlAudit.UseServiceAccount = value;
        }

        [AlsoNotifyFor(nameof(PasswordEnabled),
            nameof(ServiceAccount),
            nameof(Password))]
        public bool UseProvidedAccount
        {
            get => ServiceControlAudit.UseProvidedAccount;
            set => ServiceControlAudit.UseProvidedAccount = value;
        }

        public string ServiceAccount
        {
            get => ServiceControlAudit.ServiceAccount;
            set => ServiceControlAudit.ServiceAccount = value;
        }

        public string Password
        {
            get => ServiceControlAudit.Password;
            set => ServiceControlAudit.Password = value;
        }

        public bool PasswordEnabled
        {
            get
            {
                if (!UseProvidedAccount)
                {
                    return false;
                }

                if (ServiceAccount != null && ServiceAccount.EndsWith("$"))
                {
                    return false;
                }

                return true;
            }
        }
        public bool ManagedAccount => ServiceControlAudit.ManagedAccount;

        [AlsoNotifyFor(nameof(HostNameWarning))]
        public string HostName
        {
            get => ServiceControlAudit.HostName;
            set => ServiceControlAudit.HostName = value;
        }

        public string HostNameWarning
        {
            get => ServiceControlAudit.HostNameWarning;
            set => ServiceControlAudit.HostNameWarning = value;
        }

        public string PortNumber
        {
            get => ServiceControlAudit.PortNumber;
            set => ServiceControlAudit.PortNumber = value;
        }

        public string DatabaseMaintenancePortNumber
        {
            get => ServiceControlAudit.DatabaseMaintenancePortNumber;
            set => ServiceControlAudit.DatabaseMaintenancePortNumber = value;
        }

        public ICommand SelectLogPath
        {
            get => ServiceControlAudit.SelectLogPath;
            set => ServiceControlAudit.SelectLogPath = value;
        }

        public string LogPath
        {
            get => ServiceControlAudit.LogPath;
            set => ServiceControlAudit.LogPath = value.SanitizeFilePath();
        }

        public int MaximumAuditRetentionPeriod => ServiceControlAudit.MaximumAuditRetentionPeriod;

        public int MinimumAuditRetentionPeriod => ServiceControlAudit.MinimumAuditRetentionPeriod;

        public TimeSpanUnits AuditRetentionUnits => ServiceControlAudit.AuditRetentionUnits;

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

        [AlsoNotifyFor(nameof(AuditForwardingWarning), nameof(AuditForwardingQueueName))]
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

        public IEnumerable<EnableFullTextSearchOnBodiesOption> EnableFullTextSearchOnBodiesOptions => ServiceControlAudit.EnableFullTextSearchOnBodiesOptions;

        public EnableFullTextSearchOnBodiesOption EnableFullTextSearchOnBodies
        {
            get => ServiceControlAudit.EnableFullTextSearchOnBodies;
            set => ServiceControlAudit.EnableFullTextSearchOnBodies = value;
        }

        public bool SubmitAttempted { get; set; }
    }
}