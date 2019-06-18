namespace ServiceControl.Config.UI.InstanceEdit
{
    using System;
    using InstanceAdd;
    using PropertyChanged;
    using ServiceControlInstaller.Engine.Configuration.ServiceControl;
    using ServiceControlInstaller.Engine.Instances;
    using SharedInstanceEditor;
    using Validar;

    [InjectValidation]
    public class ServiceControlEditViewModel : ServiceControlEditorViewModel
    {
        public ServiceControlEditViewModel(ServiceControlInstance instance)
        {
            DisplayName = "EDIT SERVICECONTROL INSTANCE";
            ServiceControlInstance = instance;
            ServiceControl.UpdateFromInstance(instance);
            ErrorQueueName = instance.ErrorQueue;
            ErrorForwardingQueueName = instance.ErrorLogQueue;
            SelectedTransport = instance.TransportPackage;
            ConnectionString = instance.ConnectionString;
            ErrorForwardingVisible = instance.Version >= ServiceControlSettings.ForwardErrorMessages.SupportedFrom;
            RetentionPeriodsVisible = instance.Version >= ServiceControlSettings.ErrorRetentionPeriod.SupportedFrom;
        }

        public bool ErrorForwardingVisible { get; set; }
        public bool RetentionPeriodsVisible { get; set; }

        public ServiceControlInstance ServiceControlInstance { get; set; }

        public bool DatabaseMaintenancePortNumberRequired => ServiceControlInstance.Version >= ServiceControlSettings.DatabaseMaintenancePort.SupportedFrom;

        public string ErrorQueueName { get; set; }
        public string ErrorForwardingQueueName { get; set; }
        public string AuditQueueName { get; set; }
        public string AuditForwardingQueueName { get; set; }
        public ForwardingOption AuditForwarding { get; set; }
        public ForwardingOption ErrorForwarding { get; set; }

        [AlsoNotifyFor("AuditForwarding")]
        public string AuditForwardingWarning => AuditForwarding != null && AuditForwarding.Value ? "Only enable if another application is processing messages from the Audit Forwarding Queue" : null;

        [AlsoNotifyFor("ErrorForwarding")]
        public string ErrorForwardingWarning => ErrorForwarding != null && ErrorForwarding.Value ? "Only enable if another application is processing messages from the Error Forwarding Queue" : null;

        public bool ShowErrorForwardingCombo => ServiceControlInstance.Version >= ServiceControlSettings.ForwardErrorMessages.SupportedFrom;
        public int ErrorForwardingQueueColumn => ServiceControlInstance.Version >= ServiceControlSettings.ForwardErrorMessages.SupportedFrom ? 1 : 0;
        public int ErrorForwardingQueueColumnSpan => ServiceControlInstance.Version >= ServiceControlSettings.ForwardErrorMessages.SupportedFrom ? 1 : 2;

        public bool ShowAuditForwardingQueue //TODO: To remove
        {
            get { return true; }
        }

        public bool ShowErrorForwardingQueue //TODO: To remove
        {
            get { return true; }
        }

        public override void OnSelectedTransportChanged()
        {
            base.OnSelectedTransportChanged();
            NotifyOfPropertyChange(nameof(ErrorQueueName));
            NotifyOfPropertyChange(nameof(ErrorForwardingQueueColumn));
        }

        public void UpdateInstanceFromViewModel(ServiceControlInstance instance)
        {
            instance.HostName = ServiceControl.HostName;
            instance.Port = Convert.ToInt32(ServiceControl.PortNumber);
            instance.LogPath = ServiceControl.LogPath;
            instance.AuditLogQueue = AuditForwardingQueueName;
            instance.AuditQueue = AuditQueueName;
            instance.ErrorQueue = ErrorQueueName;
            instance.ErrorLogQueue = ErrorForwardingQueueName;
            instance.ConnectionString = ConnectionString;
            instance.DatabaseMaintenancePort = Convert.ToInt32(ServiceControl.DatabaseMaintenancePortNumber);
        }
    }
}