namespace ServiceControl.Config.UI.InstanceEdit
{
    using System;
    using InstanceAdd;
    using ServiceControlInstaller.Engine.Instances;
    using Validar;

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
        }
    }
}