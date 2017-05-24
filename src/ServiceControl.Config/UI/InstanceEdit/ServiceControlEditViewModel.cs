namespace ServiceControl.Config.UI.InstanceEdit
{
    using System;
    using System.Linq;
    using Commands;
    using PropertyChanged;
    using ServiceControlInstaller.Engine.Accounts;
    using ServiceControlInstaller.Engine.Configuration.ServiceControl;
    using ServiceControlInstaller.Engine.Instances;
    using SharedInstanceEditor;
    using Validar;

    [InjectValidation]
    public class ServiceControlEditViewModel : SharedServiceControlEditorViewModel
    {
        public ServiceControlEditViewModel(ServiceControlInstance instance) 
        {
            DisplayName = "EDIT SERVICECONTROL INSTANCE";
            SelectLogPath = new SelectPathCommand(p => LogPath = p, isFolderPicker: true, defaultPath: LogPath);

            InstanceName = instance.Name;
            Description = instance.Description;

            var userAccount = UserAccount.ParseAccountName(instance.ServiceAccount);
            UseSystemAccount = userAccount.IsLocalSystem();
            UseServiceAccount = userAccount.IsLocalService();

            UseProvidedAccount = !(UseServiceAccount || UseSystemAccount);

            if (UseProvidedAccount)
            {
                ServiceAccount = instance.ServiceAccount;
            }

            HostName = instance.HostName;
            PortNumber = instance.Port.ToString();

            LogPath = instance.LogPath;

            AuditForwardingQueueName = instance.AuditLogQueue;
            AuditQueueName = instance.AuditQueue;
            AuditForwarding = AuditForwardingOptions.FirstOrDefault(p => p.Value == instance.ForwardAuditMessages);
            ErrorForwarding = ErrorForwardingOptions.FirstOrDefault(p => p.Value == instance.ForwardErrorMessages);

            UpdateErrorRetention(instance.ErrorRetentionPeriod);
            UpdateAuditRetention(instance.AuditRetentionPeriod);

            ErrorQueueName = instance.ErrorQueue;
            ErrorForwardingQueueName = instance.ErrorLogQueue;
            SelectedTransport = Transports.First(t => StringComparer.InvariantCultureIgnoreCase.Equals(t.Name, instance.TransportPackage));
            ConnectionString = instance.ConnectionString;
            ServiceControlInstance = instance;

            ErrorForwardingVisible = instance.Version >= SettingsList.ForwardErrorMessages.SupportedFrom;

            //Both Audit and Error Retention Property was introduced in same version
            RetentionPeriodsVisible = instance.Version >= SettingsList.ErrorRetentionPeriod.SupportedFrom;
        }

        public bool ErrorForwardingVisible { get; set; }
        public bool RetentionPeriodsVisible { get; set; }

        public ServiceControlInstance ServiceControlInstance { get; set; }


        public string ErrorQueueName { get; set; }
        public string ErrorForwardingQueueName { get; set; }
        public string AuditQueueName { get; set; }
        public string AuditForwardingQueueName { get; set; }
        public ForwardingOption AuditForwarding { get; set; }
        public ForwardingOption ErrorForwarding { get; set; }

        [AlsoNotifyFor("AuditForwarding")]
        public string AuditForwardingWarning => (AuditForwarding != null && AuditForwarding.Value) ? "Only enable if another application is processing messages from the Audit Forwarding Queue" : null;

        [AlsoNotifyFor("ErrorForwarding")]
        public string ErrorForwardingWarning => (ErrorForwarding != null && ErrorForwarding.Value) ? "Only enable if another application is processing messages from the Error Forwarding Queue" : null;
        
        public bool ShowErrorForwardingCombo => ServiceControlInstance.Version >= SettingsList.ForwardErrorMessages.SupportedFrom;
        public int ErrorForwardingQueueColumn => ServiceControlInstance.Version >= SettingsList.ForwardErrorMessages.SupportedFrom ? 1 : 0;
        public int ErrorForwardingQueueColumnSpan => ServiceControlInstance.Version >= SettingsList.ForwardErrorMessages.SupportedFrom ? 1 : 2;

        public bool ShowAuditForwardingQueue
        {
            get
            {
                if (ServiceControlInstance.Version >= Compatibility.ForwardingQueuesAreOptional.SupportedFrom)
                {
                    return AuditForwarding?.Value ?? false;
                }
                return true;
            }
        }

        public bool ShowErrorForwardingQueue
        {
            get
            {
                if (ServiceControlInstance.Version >= Compatibility.ForwardingQueuesAreOptional.SupportedFrom)
                {
                    return ErrorForwarding?.Value ?? false;
                }
                return true;
            }
        }
        
        TransportInfo selectedTransport;

        [AlsoNotifyFor("ConnectionString", "ErrorQueueName", "AuditQueueName", "ErrorForwardingQueueName", "AuditForwardingQueueName")]
        public TransportInfo SelectedTransport
        {
            get { return selectedTransport; }
            set
            {
                ConnectionString = null;
                selectedTransport = value;
            }
        }

        public string TransportWarning => SelectedTransport?.Help;

        public string ConnectionString { get; set; }

        // ReSharper disable once UnusedMember.Global
        public string SampleConnectionString => SelectedTransport?.SampleConnectionString;

        // ReSharper disable once UnusedMember.Global
        public bool ShowConnectionString => !string.IsNullOrEmpty(SelectedTransport?.SampleConnectionString);
    }
}