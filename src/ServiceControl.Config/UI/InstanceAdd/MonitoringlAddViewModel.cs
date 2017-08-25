namespace ServiceControl.Config.UI.InstanceAdd
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Windows.Input;
    using PropertyChanged;
    using ServiceControl.Config.Commands;
    using ServiceControlInstaller.Engine.Instances;
    using SharedInstanceEditor;
    using Validar;

    [InjectValidation]
    public class MonitoringAddViewModel : SharedMonitoringEditorViewModel
    {
        public MonitoringAddViewModel()
        {
            DisplayName = "ADD MONITORING INSTANCE";

            SelectDestinationPath = new SelectPathCommand(p => DestinationPath = p, isFolderPicker: true, defaultPath: DestinationPath);
            SelectLogPath = new SelectPathCommand(p => LogPath = p, isFolderPicker: true, defaultPath: LogPath);

            var monitoringInstances = InstanceFinder.MonitoringInstances();
            if (!monitoringInstances.Any())
            {
                InstanceName = "Particular.Monitoring";
                PortNumber = "33633";
            }
            else
            {
                var i = 0;
                while (true)
                {
                    InstanceName = $"Particular.Monitoring.{++i}";
                    if (!monitoringInstances.Any(p => p.Name.Equals(InstanceName, StringComparison.OrdinalIgnoreCase)))
                    {
                        break;
                    }
                }
            }
            Description = "A Monitoring Instance";
            HostName = "localhost"; 
            ErrorQueueName = "error";
            UseSystemAccount = true;
        }

        public string DestinationPath { get; set; }
        public ICommand SelectDestinationPath { get; private set; }

        protected override void OnInstanceNameChanged()
        {
            DestinationPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Particular Software", InstanceName);
            LogPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Particular", "ServiceControl", InstanceName, "Logs");
        }

        public string ErrorQueueName { get; set; }

        TransportInfo selectedTransport;

        [AlsoNotifyFor("ConnectionString", "ErrorQueueName")]
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