namespace ServiceControl.Config.UI.SharedInstanceEditor
{
    using System;
    using System.Collections.Generic;
    using System.Windows;
    using System.Windows.Input;
    using PropertyChanged;
    using ServiceControl.Config.Framework.Rx;
    using ServiceControl.Config.Validation;
    using ServiceControlInstaller.Engine.Instances;

    public class BoolDisplayObject
    {
        public string Name { get; set; }
        public bool? Value { get; set; }
    }

    public class SharedInstanceEditorViewModel : RxProgressScreen
    {
        string hostName;
        string serviceAccount;
        string password;
        TransportInfo selectedTransport;

        public SharedInstanceEditorViewModel()
        {
            Transports = ServiceControlInstaller.Engine.Instances.Transports.All;
        }

        [DoNotNotify]
        public ValidationTemplate ValidationTemplate { get; set; }

        public string InstanceName { get; set; }

        public string HostName
        {
            get { return hostName; }
            set
            {
                if (!string.Equals("localhost", value, StringComparison.InvariantCulture))
                {
                    HostNameWarning = Validations.WRN_HOSTNAME_SHOULD_BE_LOCALHOST;
                }
                else
                {
                    HostNameWarning = string.Empty;
                }

                hostName = value;
            }
        }

        public string HostNameWarning { get; set; }

        public string PortNumber { get; set; }

        public string Description { get; set; }

        public string ServiceAccount
        {
            get { return UseProvidedAccount ? serviceAccount : "System"; }
            set { serviceAccount = value; }
        }

        public string Password
        {
            get { return UseProvidedAccount ? password : ""; }
            set { password = value; }
        }

        public bool UseSystemAccount { get { return !UseProvidedAccount; } }
        public bool UseProvidedAccount { get; set; }

        public string ErrorQueueName { get; set; }
        public string ErrorForwardingQueueName { get; set; }
        public string AuditQueueName { get; set; }
        public bool? AuditForwarding { get; set; }
        public string AuditForwardingQueueName { get; set; }

        public IEnumerable<BoolDisplayObject> ComboItems
        {
            get
            {
                return new[]
                {
                    new BoolDisplayObject() { Name = "On", Value = true },
                    new BoolDisplayObject() { Name = "Off", Value = false }
                };
            }
        }

        public IEnumerable<TransportInfo> Transports { get; private set; }

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

        public string ConnectionString { get; set; }
        public string SampleConnectionString { get { return SelectedTransport != null ? SelectedTransport.SampleConnectionString : ""; } }
        public bool ShowConnectionString { get { return SelectedTransport != null && !string.IsNullOrEmpty(SelectedTransport.SampleConnectionString); } }

        public string LogPath { get; set; }
        public ICommand SelectLogPath { get; set; }

        public ICommand Save { get; set; }
        public ICommand Cancel { get; set; }

        protected virtual void OnInstanceNameChanged()
        {
        }
    }
}