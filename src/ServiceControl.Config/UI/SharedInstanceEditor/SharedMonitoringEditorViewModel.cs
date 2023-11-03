

namespace ServiceControl.Config.UI.SharedInstanceEditor
{
    using System;
    using System.Collections.Generic;
    using System.Windows.Input;
    using Framework.Rx;
    using PropertyChanged;
    using ServiceControl.Config.Extensions;
    using ServiceControlInstaller.Engine.Instances;
    using Validation;
    using Validations = Validation.Validations;

    public class SharedMonitoringEditorViewModel : RxProgressScreen
    {
        public SharedMonitoringEditorViewModel()
        {
            Transports = ServiceControlCoreTransports.GetSupportedTransports();
        }

        [DoNotNotify]
        public ValidationTemplate ValidationTemplate { get; set; }

        public string InstanceName
        {
            get => instanceName;
            set => instanceName = value.SanitizeInstanceName();
        }

        public string HostName
        {
            get => hostName;
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
            set => serviceAccount = value;
        }

        public string Password
        {
            get { return UseProvidedAccount ? password : string.Empty; }
            set { password = value; }
        }
        [AlsoNotifyFor(nameof(PasswordEnabled),
          nameof(ServiceAccount),
          nameof(Password))]
        public bool UseSystemAccount
        {
            get => useSystemAccount;
            set
            {
                useSystemAccount = value;

                if (value)
                {
                    UseServiceAccount = false;
                    UseProvidedAccount = false;
                    ServiceAccount = "LocalSystem";
                    Password = null;
                }
            }
        }

        public bool UseServiceAccount
        {
            get => useServiceAccount;
            set
            {
                useServiceAccount = value;

                if (value)
                {
                    UseSystemAccount = false;
                    UseProvidedAccount = false;
                    ServiceAccount = "LocalService";
                    Password = null;
                }
            }
        }

        public bool UseProvidedAccount
        {
            get => useProvidedAccount;
            set
            {
                useProvidedAccount = value;

                if (value)
                {
                    UseServiceAccount = false;
                    UseSystemAccount = false;
                    ServiceAccount = null;
                    Password = null;
                }
            }
        }

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

        public string LogPath
        {
            get => logPath;
            set => logPath = value.SanitizeFilePath();
        }
        public ICommand SelectLogPath { get; set; }

        public ICommand Save { get; set; }
        public ICommand Cancel { get; set; }

        public bool SubmitAttempted { get; set; }

        protected virtual void OnInstanceNameChanged()
        {
        }

        string instanceName;

        string hostName;

        string serviceAccount;

        string password;

        bool useSystemAccount;

        bool useServiceAccount;

        bool useProvidedAccount;

        string logPath;
    }
}