namespace ServiceControl.Config.UI.InstanceEdit
{
    using System;
    using System.Linq;
    using Commands;
    using ServiceControlInstaller.Engine.Instances;
    using SharedInstanceEditor;
    using Validar;

    [InjectValidation]
    public class InstanceEditViewModel : SharedInstanceEditorViewModel
    {
        public InstanceEditViewModel(ServiceControlInstance instance)
        {
            DisplayName = "Edit Instance";
            SelectLogPath = new SelectPathCommand(p => LogPath = p, isFolderPicker: true, defaultPath: LogPath);

            InstanceName = instance.Name;
            Description = instance.Description;

            UseProvidedAccount = !StringComparer.OrdinalIgnoreCase.Equals(instance.ServiceAccount, "localsystem");
            if (UseProvidedAccount)
            {
                ServiceAccount = instance.ServiceAccount;
                Password = instance.ServiceAccountPwd;
            }

            HostName = instance.HostName;
            PortNumber = instance.Port.ToString();

            LogPath = instance.LogPath;
            // instance.VirtualDirectory
            AuditForwardingQueueName = instance.AuditLogQueue;
            AuditQueueName = instance.AuditQueue;
            AuditForwarding = ForwardingOptions.FirstOrDefault(p => p.Value == instance.ForwardAuditMessages);
            ErrorForwarding = ForwardingOptions.FirstOrDefault(p => p.Value == instance.ForwardErrorMessages);
            ErrorQueueName = instance.ErrorQueue;
            ErrorForwardingQueueName = instance.ErrorLogQueue;
            SelectedTransport = Transports.First(t => StringComparer.InvariantCultureIgnoreCase.Equals(t.Name, instance.TransportPackage));
            ConnectionString = instance.ConnectionString;
            ServiceControlInstance = instance;
        }

        public ServiceControlInstance ServiceControlInstance { get; set; }
    }
}