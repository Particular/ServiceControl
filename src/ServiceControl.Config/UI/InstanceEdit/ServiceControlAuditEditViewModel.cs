namespace ServiceControl.Config.UI.InstanceEdit
{
    using System;
    using System.Collections.Generic;
    using InstanceAdd;
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
            InstallErrorInstance = false;
            InstallAuditInstance = false;
        }
        public ServiceControlAuditEditViewModel()
        {
            InstallAuditInstance = false;
            InstallErrorInstance = false;
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

        public bool UseSystemAccount
        {
            get => ServiceControlAudit.UseSystemAccount;
            set => ServiceControlAudit.UseSystemAccount = value;
        }

        public bool UseServiceAccount
        {
            get => ServiceControlAudit.UseServiceAccount;
            set => ServiceControlAudit.UseServiceAccount = value;
        }

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

        public bool PasswordEnabled => ServiceControlAudit.PasswordEnabled;

        public bool ManagedAccount => ServiceControlAudit.ManagedAccount;

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

        public System.Windows.Input.ICommand SelectLogPath
        {
            get => ServiceControlAudit.SelectLogPath;
            set => ServiceControlAudit.SelectLogPath = value;
        }

        public string LogPath
        {
            get => ServiceControlAudit.LogPath;
            set => ServiceControlAudit.LogPath = value;
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

        public bool ShowAuditForwardingQueue => ServiceControlAudit.ShowAuditForwardingQueue;

        public string AuditForwardingWarning => ServiceControlAudit.AuditForwardingWarning;

        public IEnumerable<EnableFullTextSearchOnBodiesOption> EnableFullTextSearchOnBodiesOptions => ServiceControlAudit.EnableFullTextSearchOnBodiesOptions;

        public EnableFullTextSearchOnBodiesOption EnableFullTextSearchOnBodies
        {
            get => ServiceControlAudit.EnableFullTextSearchOnBodies;
            set => ServiceControlAudit.EnableFullTextSearchOnBodies = value;
        }
    }
}