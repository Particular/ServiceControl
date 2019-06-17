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
    public class ServiceControlAuditEditViewModel : ServiceControlEditorViewModel
    {
        public ServiceControlAuditEditViewModel(ServiceControlAuditInstance instance)
        {
            DisplayName = "EDIT SERVICECONTROL AUDIT INSTANCE";
            ServiceControlInstance = instance;
            ServiceControlAudit.UpdateFromInstance(instance);
            ErrorQueueName = instance.ErrorQueue;
            ErrorForwardingQueueName = instance.ErrorLogQueue;
            SelectedTransport = instance.TransportPackage;
            ConnectionString = instance.ConnectionString;
            ErrorForwardingVisible = instance.Version >= SettingsList.ForwardErrorMessages.SupportedFrom;
            RetentionPeriodsVisible = instance.Version >= SettingsList.ErrorRetentionPeriod.SupportedFrom;
        }

        public bool ErrorForwardingVisible { get; set; }
        public bool RetentionPeriodsVisible { get; set; }

        public ServiceControlAuditInstance ServiceControlInstance { get; set; }

        public bool DatabaseMaintenancePortNumberRequired => ServiceControlInstance.Version >= SettingsList.DatabaseMaintenancePort.SupportedFrom;

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

        public override void OnSelectedTransportChanged()
        {
            base.OnSelectedTransportChanged();
            NotifyOfPropertyChange(nameof(AuditQueueName));
            NotifyOfPropertyChange(nameof(AuditForwardingQueueName));
        }

        public void UpdateInstanceFromViewModel(ServiceControlAuditInstance instance)
        {
            instance.HostName = ServiceControlAudit.HostName;
            instance.Port = Convert.ToInt32(ServiceControlAudit.PortNumber);
            instance.LogPath = ServiceControlAudit.LogPath;
            instance.AuditLogQueue = AuditForwardingQueueName;
            instance.AuditQueue = AuditQueueName;
            instance.ErrorQueue = ErrorQueueName;
            instance.ErrorLogQueue = ErrorForwardingQueueName;
            instance.ConnectionString = ConnectionString;

            if (ServiceControlInstance.Version.Major >= 2)
            {
                instance.DatabaseMaintenancePort = Convert.ToInt32(ServiceControlAudit.DatabaseMaintenancePortNumber);
            }
        }
    }
}