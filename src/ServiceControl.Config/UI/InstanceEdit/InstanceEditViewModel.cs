namespace ServiceControl.Config.UI.InstanceEdit
{
    using System;
    using System.Linq;
    using Commands;
    using ServiceControlInstaller.Engine.Configuration;
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
            
            AuditForwardingQueueName = instance.AuditLogQueue;
            AuditQueueName = instance.AuditQueue;
            AuditForwarding = AuditForwardingOptions.FirstOrDefault(p => p.Value == instance.ForwardAuditMessages);
            ErrorForwarding = ErrorForwardingOptions.FirstOrDefault(p => p.Value == instance.ForwardErrorMessages);

            ErrorRetentionPeriod = instance.ErrorRetentionPeriod;
            AuditRetentionPeriod = instance.AuditRetentionPeriod;

            ErrorQueueName = instance.ErrorQueue;
            ErrorForwardingQueueName = instance.ErrorLogQueue;
            SelectedTransport = Transports.First(t => StringComparer.InvariantCultureIgnoreCase.Equals(t.Name, instance.TransportPackage));
            ConnectionString = instance.ConnectionString;
            ServiceControlInstance = instance;

            ErrorForwardingVisible = instance.Version > SettingsList.ForwardErrorMessages.SupportedFrom;
            ErrorRetentionPeriodVisible = instance.Version > SettingsList.ErrorRetentionPeriod.SupportedFrom;
            AuditRetentionPeriodVisible = instance.Version > SettingsList.AuditRetentionPeriod.SupportedFrom;
        }

        public bool ErrorForwardingVisible { get; set; }
        public bool ErrorRetentionPeriodVisible { get; set; }
        public bool AuditRetentionPeriodVisible { get; set; }

        public ServiceControlInstance ServiceControlInstance { get; set; }
    }
}