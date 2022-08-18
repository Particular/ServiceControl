namespace ServiceControl.Config.UI.SharedInstanceEditor
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Windows.Input;
    using Commands;
    using Framework.Rx;
    using ServiceControlInstaller.Engine.Accounts;
    using ServiceControlInstaller.Engine.Instances;
    using Validation;

    public class SharedServiceControlEditorViewModel : RxScreen
    {
        public SharedServiceControlEditorViewModel()
        {
            SelectDestinationPath = new SelectPathCommand(p => DestinationPath = p, isFolderPicker: true, defaultPath: DestinationPath);
            SelectDatabasePath = new SelectPathCommand(p => DatabasePath = p, isFolderPicker: true, defaultPath: DatabasePath);
            SelectLogPath = new SelectPathCommand(p => LogPath = p, isFolderPicker: true, defaultPath: LogPath);
        }

        public string DestinationPath { get; set; }
        public ICommand SelectDestinationPath { get; }

        public string DatabasePath { get; set; }
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

        public string Password
        {
            get { return UseProvidedAccount ? password : string.Empty; }
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

        public string LogPath { get; set; }
        public ICommand SelectLogPath { get; set; }

        protected void OnInstanceNameChanged()
        {
            DestinationPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Particular Software", InstanceName);
            DatabasePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Particular", "ServiceControl", InstanceName, "DB");
            LogPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Particular", "ServiceControl", InstanceName, "Logs");
        }

        protected string GetConventionalServiceName(string suggestedName)
        {
            var instanceName = string.Empty;
            var instanceCount = GetInstalledInstancesCount();
            var titleCaseName = CultureInfo.CurrentUICulture.TextInfo.ToTitleCase(suggestedName);
            var serviceBaseName = instanceCount == 0 ? "ServiceControl" : "ServiceControl-" + instanceCount;

            if (!suggestedName.StartsWith("Particular.", StringComparison.InvariantCultureIgnoreCase))
            {
                instanceName += "Particular.";
            }

            instanceName += !string.IsNullOrEmpty(suggestedName) ? titleCaseName : serviceBaseName;

            return RemoveIllegalCharacters(instanceName);
        }

        protected string RemoveIllegalCharacters(string name)
        {
            return name?.Replace(' ', '.');
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
        string serviceAccount;
        string password;
    }
}