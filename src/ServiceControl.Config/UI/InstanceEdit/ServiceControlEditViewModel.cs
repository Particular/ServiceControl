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
    public class ServiceControlEditViewModel : ServiceControlEditorViewModel
    {
        public ServiceControlEditViewModel(ServiceControlInstance instance)
        {
            DisplayName = "EDIT SERVICECONTROL INSTANCE";
            ServiceControlInstance = instance;
            ServiceControl.UpdateFromInstance(instance);
            SelectedTransport = instance.TransportPackage;
            ConnectionString = instance.ConnectionString;
        }

        //This is used for unit testing
        internal ServiceControlEditViewModel()
        {

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

        [AlsoNotifyFor(nameof(HostNameWarning))]
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

        [AlsoNotifyFor(
            nameof(PasswordEnabled),
            nameof(ServiceAccount),
            nameof(Password))]
        public bool UseSystemAccount
        {
            get => ServiceControl.UseSystemAccount;
            set => ServiceControl.UseSystemAccount = value;
        }

        [AlsoNotifyFor(
            nameof(PasswordEnabled),
            nameof(ServiceAccount),
            nameof(Password))]
        public bool UseServiceAccount
        {
            get => ServiceControl.UseServiceAccount;
            set => ServiceControl.UseServiceAccount = value;
        }

        [AlsoNotifyFor(nameof(PasswordEnabled),
            nameof(ServiceAccount),
            nameof(Password))]
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
            set => ServiceControl.LogPath = value.SanitizeFilePath();
        }

        public ICommand SelectLogPath
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

        public bool ShowErrorForwardingQueue => ErrorForwarding?.Value ?? false;

        public string ErrorQueueName
        {
            get => ServiceControl.ErrorQueueName;
            set => ServiceControl.ErrorQueueName = value;
        }

        public IEnumerable<ForwardingOption> ErrorForwardingOptions => ServiceControl.ErrorForwardingOptions;

        [AlsoNotifyFor(nameof(ErrorForwardingWarning), nameof(ErrorForwardingQueueName))]
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

        public bool SubmitAttempted { get; set; }
    }
}