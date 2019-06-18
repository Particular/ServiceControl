namespace ServiceControl.Config.UI.InstanceEdit
{
    using System;
    using InstanceAdd;
    using ServiceControlInstaller.Engine.Instances;
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

        public void UpdateInstanceFromViewModel(ServiceControlAuditInstance instance)
        {
            instance.HostName = ServiceControlAudit.HostName;
            instance.Port = Convert.ToInt32(ServiceControlAudit.PortNumber);
            instance.LogPath = ServiceControlAudit.LogPath;
            instance.AuditLogQueue = ServiceControlAudit.AuditForwardingQueueName;
            instance.AuditQueue = ServiceControlAudit.AuditQueueName;
            instance.AuditRetentionPeriod = ServiceControlAudit.AuditRetentionPeriod;
            instance.ForwardAuditMessages = ServiceControlAudit.AuditForwarding.Value;
            instance.LogPath = ServiceControlAudit.LogPath;
            instance.ConnectionString = ConnectionString;
            instance.DatabaseMaintenancePort = Convert.ToInt32(ServiceControlAudit.DatabaseMaintenancePortNumber);
        }
    }
}