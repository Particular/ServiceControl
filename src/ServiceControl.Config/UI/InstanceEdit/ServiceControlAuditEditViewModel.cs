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
            SelectedTransport = instance.TransportPackage;
            ConnectionString = instance.ConnectionString;
        }

        public ServiceControlAuditInstance ServiceControlInstance { get; set; }

        public bool DatabaseMaintenancePortNumberRequired => ServiceControlInstance.Version >= AuditInstanceSettingsList.DatabaseMaintenancePort.SupportedFrom;

        public string AuditQueueName { get; set; }

        public string AuditForwardingQueueName { get; set; }

        public ForwardingOption AuditForwarding { get; set; }
        
        [AlsoNotifyFor("AuditForwarding")]
        public string AuditForwardingWarning => AuditForwarding != null && AuditForwarding.Value ? "Only enable if another application is processing messages from the Audit Forwarding Queue" : null;

        public bool ShowAuditForwardingQueue => AuditForwarding?.Value ?? false;

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
            instance.ConnectionString = ConnectionString;
            instance.DatabaseMaintenancePort = Convert.ToInt32(ServiceControlAudit.DatabaseMaintenancePortNumber);
        }
    }
}