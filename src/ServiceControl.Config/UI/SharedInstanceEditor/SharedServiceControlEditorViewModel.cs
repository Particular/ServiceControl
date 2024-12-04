namespace ServiceControl.Config.UI.SharedInstanceEditor
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Windows.Input;
    using Commands;
    using Extensions;
    using Framework.Rx;
    using ServiceControlInstaller.Engine.Accounts;
    using ServiceControlInstaller.Engine.Instances;
    using Validations = Validation.Validations;

    public class SharedServiceControlEditorViewModel : RxScreen
    {
        public SharedServiceControlEditorViewModel()
        {
            SelectDestinationPath = new SelectPathCommand(p => DestinationPath = p, isFolderPicker: true, defaultPath: DestinationPath);
            SelectDatabasePath = new SelectPathCommand(p => DatabasePath = p, isFolderPicker: true, defaultPath: DatabasePath);
            SelectLogPath = new SelectPathCommand(p => LogPath = p, isFolderPicker: true, defaultPath: LogPath);
        }

        public string DestinationPath
        {
            get => destinationPath;
            set => destinationPath = value.SanitizeFilePath();
        }
        public ICommand SelectDestinationPath { get; }

        public string DatabasePath
        {
            get => databasePath;
            set => databasePath = value.SanitizeFilePath();
        }
        public ICommand SelectDatabasePath { get; }

        public bool SubmitAttempted { get; set; }
        public string InstanceName { get; set; }

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

        public string DatabaseMaintenancePortNumber { get; set; }

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

        public string Password
        {
            get { return UseProvidedAccount ? password : string.Empty; }
            set { password = value; }
        }

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

        public void SetupServiceAccount(ServiceControlBaseService instance)
        {
            var userAccount = UserAccount.ParseAccountName(instance.ServiceAccount);
            UseSystemAccount = userAccount.IsLocalSystem();
            UseServiceAccount = userAccount.IsLocalService();
            UseProvidedAccount = !(UseServiceAccount || UseSystemAccount);

            if (UseProvidedAccount)
            {
                ServiceAccount = instance.ServiceAccount;
            }
        }

        public string LogPath
        {
            get => logPath;
            set => logPath = value.SanitizeFilePath();
        }

        public ICommand SelectLogPath { get; set; }

        protected void OnInstanceNameChanged()
        {
            DestinationPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Particular Software", InstanceName);
            DatabasePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Particular", "ServiceControl", InstanceName, "DB");
            LogPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Particular", "ServiceControl", InstanceName, "Logs");
        }

        protected string GetConventionalServiceName(string conventionName)
        {
            var instanceName = string.Empty;

            var instanceCount = GetInstalledInstancesCount();

            var validServiceName = conventionName.SanitizeInstanceName();

            var serviceBaseName = instanceCount == 0 ? "ServiceControl" : "ServiceControl-" + instanceCount;

            if (!conventionName.StartsWith("Particular.", StringComparison.InvariantCultureIgnoreCase))
            {
                instanceName += "Particular.";
            }

            instanceName += !string.IsNullOrEmpty(conventionName) ? validServiceName : serviceBaseName;

            return instanceName;
        }

        protected int GetInstalledInstancesCount()
        {
            var serviceControlInstances = InstanceFinder.ServiceControlInstances();

            if (!serviceControlInstances.Any())
            {
                return 0;
            }

            var i = 0;
            while (true)
            {
                i++;
                if (!serviceControlInstances.Any(p => p.Name.Equals(InstanceName, StringComparison.OrdinalIgnoreCase)))
                {
                    return i;
                }
            }
        }


        string hostName;

        bool useSystemAccount;

        bool useServiceAccount;

        bool useProvidedAccount;

        string serviceAccount;

        string password;

        string destinationPath;

        string logPath;

        string databasePath;

    }
}