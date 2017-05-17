namespace ServiceControl.Config.UI.InstanceAdd
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Windows.Input;
    using PropertyChanged;
    using ServiceControl.Config.Commands;
    using ServiceControlInstaller.Engine.Configuration.ServiceControl;
    using ServiceControlInstaller.Engine.Instances;
    using SharedInstanceEditor;
    using Validar;

    [InjectValidation]
    public class ServiceControlAddViewModel : SharedServiceControlEditorViewModel
    {
        public ServiceControlAddViewModel()
        {
            DisplayName = "Add new instance";

            SelectDestinationPath = new SelectPathCommand(p => DestinationPath = p, isFolderPicker: true, defaultPath: DestinationPath);
            SelectDatabasePath = new SelectPathCommand(p => DatabasePath = p, isFolderPicker: true, defaultPath: DatabasePath);
            SelectLogPath = new SelectPathCommand(p => LogPath = p, isFolderPicker: true, defaultPath: LogPath);

            var serviceControlInstances = InstanceFinder.ServiceControlInstances();
            if (!serviceControlInstances.Any())
            {
                InstanceName = "Particular.ServiceControl";
                PortNumber = "33333";
            }
            else
            {
                var i = 0;
                while (true)
                {
                    InstanceName = $"Particular.ServiceControl.{++i}";
                    if (!serviceControlInstances.Any(p => p.Name.Equals(InstanceName, StringComparison.OrdinalIgnoreCase)))
                    {
                        break;
                    }
                }
            }

            AuditRetention = SettingConstants.AuditRetentionPeriodDefaultInHoursForUI;
            ErrorRetention = SettingConstants.ErrorRetentionPeriodDefaultInDaysForUI;
            Description = "A ServiceControl Instance";
            HostName = "localhost"; 
            AuditQueueName = "audit";
            AuditForwardingQueueName = "audit.log";
            ErrorQueueName = "error";
            ErrorForwardingQueueName = "error.log";
            ErrorForwarding = ErrorForwardingOptions.First(p => !p.Value); //Default to Off.
            UseSystemAccount = true;
        }

        public string DestinationPath { get; set; }
        public ICommand SelectDestinationPath { get; private set; }

        public string DatabasePath { get; set; }
        public ICommand SelectDatabasePath { get; private set; }
        
        protected override void OnInstanceNameChanged()
        {
            DestinationPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Particular Software", InstanceName);
            DatabasePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Particular", "ServiceControl", InstanceName, "DB");
            LogPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Particular", "ServiceControl", InstanceName, "Logs");
        }

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

        public bool ShowAuditForwardingQueue => AuditForwarding?.Value ?? false;
        public bool ShowErrorForwardingQueue => ErrorForwarding?.Value ?? false;
                    
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