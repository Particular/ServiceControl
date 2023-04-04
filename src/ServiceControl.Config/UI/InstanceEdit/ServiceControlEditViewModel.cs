namespace ServiceControl.Config.UI.InstanceEdit
{
    using System;
    using System.Collections.Generic;
    using InstanceAdd;
    using ServiceControlInstaller.Engine.Instances;
    using Validar;
    using Xaml.Controls;

    [InjectValidation]
    public class ServiceControlEditViewModel : ServiceControlEditorViewModel
    {
        public ServiceControlEditViewModel(ServiceControlInstance instance)
        {
            DisplayName = "EDIT SERVICECONTROL INSTANCE";
            ServiceControlInstance = instance;
            ServiceControl.UpdateFromInstance(instance);
            SelectedTransport = instance.TransportPackage;
            ConnectionString = instance.ConnectionString;
            InstallAuditInstance = false;
            InstallErrorInstance = false;
        }

        public ServiceControlEditViewModel()
        {
            InstallAuditInstance = false;
            InstallErrorInstance = false;
        }

        public ServiceControlInstance ServiceControlInstance { get; set; }

        public void UpdateInstanceFromViewModel(ServiceControlInstance instance)
        {
            instance.HostName = ServiceControl.HostName;
            instance.Port = Convert.ToInt32(ServiceControl.PortNumber);
            instance.LogPath = ServiceControl.LogPath;
            instance.ErrorQueue = ServiceControl.ErrorQueueName;
            instance.ErrorLogQueue = ServiceControl.ErrorForwardingQueueName;
            instance.ErrorRetentionPeriod = ServiceControl.ErrorRetentionPeriod;
            instance.ForwardErrorMessages = ServiceControl.ErrorForwarding.Value;
            instance.ConnectionString = ConnectionString;
            instance.DatabaseMaintenancePort = Convert.ToInt32(ServiceControl.DatabaseMaintenancePortNumber);
            instance.EnableFullTextSearchOnBodies = ServiceControl.EnableFullTextSearchOnBodies.Value;
        }

        public string InstanceName => ServiceControl.InstanceName;

        public string HostName
        {
            get => ServiceControl.HostName;
            set => ServiceControl.HostName = value;
        }

        public string HostNameWarning
        {
            get => ServiceControl.HostNameWarning;
            set => ServiceControl.HostNameWarning = value;
        }

        public bool UseSystemAccount
        {
            get => ServiceControl.UseSystemAccount;
            set => ServiceControl.UseSystemAccount = value;
        }

        public bool UseServiceAccount
        {
            get => ServiceControl.UseServiceAccount;
            set => ServiceControl.UseServiceAccount = value;
        }

        public bool UseProvidedAccount
        {
            get => ServiceControl.UseProvidedAccount;
            set => ServiceControl.UseProvidedAccount = value;
        }

        public string ServiceAccount
        {
            get => ServiceControl.ServiceAccount;
            set => ServiceControl.ServiceAccount = value;
        }

        public string Password
        {
            get => ServiceControl.Password;
            set => ServiceControl.Password = value;
        }

        public bool PasswordEnabled => ServiceControl.PasswordEnabled;

        public bool ManagedAccount => ServiceControl.ManagedAccount;

        public string PortNumber
        {
            get => ServiceControl.PortNumber;
            set => ServiceControl.PortNumber = value;
        }

        public string DatabaseMaintenancePortNumber
        {
            get => ServiceControl.DatabaseMaintenancePortNumber;
            set => ServiceControl.DatabaseMaintenancePortNumber = value;
        }

        public string LogPath
        {
            get => ServiceControl.LogPath;
            set => ServiceControl.LogPath = value;
        }

        public System.Windows.Input.ICommand SelectLogPath
        {
            get => ServiceControl.SelectLogPath;
            set => ServiceControl.SelectLogPath = value;
        }

        public int MinimumErrorRetentionPeriod => ServiceControl.MinimumErrorRetentionPeriod;

        public int MaximumErrorRetentionPeriod => ServiceControl.MaximumErrorRetentionPeriod;

        public TimeSpanUnits ErrorRetentionUnits => ServiceControl.ErrorRetentionUnits;

        public double ErrorRetention
        {
            get => ServiceControl.ErrorRetention;
            set => ServiceControl.ErrorRetention = value;
        }

        public bool ShowErrorForwardingQueue => ServiceControl.ShowErrorForwardingQueue;

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

        public string ErrorForwardingWarning => ServiceControl.ErrorForwardingWarning;

        public string ErrorForwardingQueueName
        {
            get => ServiceControl.ErrorForwardingQueueName;
            set => ServiceControl.ErrorForwardingQueueName = value;
        }

        public IEnumerable<EnableFullTextSearchOnBodiesOption> EnableFullTextSearchOnBodiesOptions =>
            ServiceControl.EnableFullTextSearchOnBodiesOptions;

        public EnableFullTextSearchOnBodiesOption EnableFullTextSearchOnBodies
        {
            get => ServiceControl.EnableFullTextSearchOnBodies;
            set => ServiceControl.EnableFullTextSearchOnBodies = value;
        }
    }
}