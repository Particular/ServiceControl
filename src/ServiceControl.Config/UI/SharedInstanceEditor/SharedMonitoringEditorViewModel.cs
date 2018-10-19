namespace ServiceControl.Config.UI.SharedInstanceEditor
{
    using System;
    using System.Collections.Generic;
    using System.Windows.Input;
    using Framework.Rx;
    using PropertyChanged;
    using ServiceControlInstaller.Engine.Instances;
    using Validation;

    public class SharedMonitoringEditorViewModel : RxProgressScreen
    {
        public SharedMonitoringEditorViewModel()
        {
            Transports = MonitoringTransports.All;
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
            get
            {
                if (UseProvidedAccount)
                {
                    return serviceAccount;
                }

                if (UseSystemAccount)
                {
                    return "LocalSystem";
                }

                return "LocalService";
            }
            set { serviceAccount = value; }
        }

        public string Password
        {
            get { return UseProvidedAccount ? password : String.Empty; }
            set { password = value; }
        }

        public bool UseSystemAccount { get; set; }

        public bool UseServiceAccount { get; set; }

        public bool UseProvidedAccount { get; set; }

        public bool PasswordEnabled
        {
            get
            {
                if (!UseProvidedAccount)
                {
                    return false;
                }

                if (ServiceAccount != null && ServiceAccount.EndsWith("$"))
                {
                    return false;
                }

                return true;
            }
        }

        public bool ManagedAccount
        {
            get
            {
                var managedAccount = UseProvidedAccount && ServiceAccount != null && ServiceAccount.Trim().EndsWith("$");
                if (managedAccount)
                {
                    Password = null;
                }

                return managedAccount;
            }
        }


        public IEnumerable<TransportInfo> Transports { get; private set; }

        public string LogPath { get; set; }
        public ICommand SelectLogPath { get; set; }

        public ICommand Save { get; set; }
        public ICommand Cancel { get; set; }

        public bool SubmitAttempted { get; set; }

        protected virtual void OnInstanceNameChanged()
        {
        }

        string hostName;
        string serviceAccount;
        string password;
    }
}