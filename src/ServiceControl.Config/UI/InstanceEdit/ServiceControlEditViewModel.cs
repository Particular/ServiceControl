namespace ServiceControl.Config.UI.InstanceEdit
{
    using System;
    using InstanceAdd;
    using PropertyChanged;
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
        }

        public ServiceControlInstance ServiceControlInstance { get; set; }

        public string ErrorQueueName { get; set; }
        public string ErrorForwardingQueueName { get; set; }
        public ForwardingOption ErrorForwarding { get; set; }

        [AlsoNotifyFor("ErrorForwarding")]
        public string ErrorForwardingWarning => ErrorForwarding != null && ErrorForwarding.Value ? "Only enable if another application is processing messages from the Error Forwarding Queue" : null;

        public override void OnSelectedTransportChanged()
        {
            base.OnSelectedTransportChanged();
            NotifyOfPropertyChange(nameof(ErrorQueueName));
        }

        public void UpdateInstanceFromViewModel(ServiceControlInstance instance)
        {
            instance.HostName = ServiceControl.HostName;
            instance.Port = Convert.ToInt32(ServiceControl.PortNumber);
            instance.LogPath = ServiceControl.LogPath;
            instance.ErrorQueue = ErrorQueueName;
            instance.ErrorLogQueue = ErrorForwardingQueueName;
            instance.ConnectionString = ConnectionString;
            instance.DatabaseMaintenancePort = Convert.ToInt32(ServiceControl.DatabaseMaintenancePortNumber);
        }
    }
}