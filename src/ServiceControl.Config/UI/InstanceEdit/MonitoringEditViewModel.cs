﻿namespace ServiceControl.Config.UI.InstanceEdit
{
    using System;
    using Commands;
    using PropertyChanged;
    using ServiceControlInstaller.Engine.Accounts;
    using ServiceControlInstaller.Engine.Instances;
    using SharedInstanceEditor;
    using Validar;

    [InjectValidation]
    public class MonitoringEditViewModel : SharedMonitoringEditorViewModel
    {
        public MonitoringEditViewModel(MonitoringInstance instance)
        {
            DisplayName = "EDIT MONITORING INSTANCE";
            SelectLogPath = new SelectPathCommand(p => LogPath = p, isFolderPicker: true, defaultPath: LogPath);

            InstanceName = instance.Name;
            Description = instance.Description;

            var userAccount = UserAccount.ParseAccountName(instance.ServiceAccount);
            UseSystemAccount = userAccount.IsLocalSystem();
            UseServiceAccount = userAccount.IsLocalService();

            UseProvidedAccount = !(UseServiceAccount || UseSystemAccount);

            if (UseProvidedAccount)
            {
                ServiceAccount = instance.ServiceAccount;
            }

            HostName = instance.HostName;
            PortNumber = instance.Port.ToString();
            LogPath = instance.LogPath;
            ErrorQueueName = instance.ErrorQueue;
            SelectedTransport = instance.TransportPackage;
            ConnectionString = instance.ConnectionString;
            MonitoringInstance = instance;
        }
        //Used for unit testing only
        internal MonitoringEditViewModel()
        {
        }
        public MonitoringInstance MonitoringInstance { get; set; }

        public string ErrorQueueName { get; set; }

        [AlsoNotifyFor("ConnectionString", "ErrorQueueName")]
        public TransportInfo SelectedTransport
        {
            get => selectedTransport;
            set
            {
                ConnectionString = null;
                selectedTransport = value;
            }
        }

        public string TransportWarning => SelectedTransport?.Help;

        public string ConnectionString { get; set; }

        public string SampleConnectionString => SelectedTransport?.SampleConnectionString;

        public bool ShowConnectionString => !string.IsNullOrEmpty(SelectedTransport?.SampleConnectionString);


        public void UpdateInstanceFromViewModel(MonitoringInstance instance)
        {
            instance.HostName = HostName;
            instance.Port = Convert.ToInt32(PortNumber);
            instance.LogPath = LogPath;
            instance.ErrorQueue = ErrorQueueName;
            instance.ConnectionString = ConnectionString;
        }

        TransportInfo selectedTransport;
    }
}