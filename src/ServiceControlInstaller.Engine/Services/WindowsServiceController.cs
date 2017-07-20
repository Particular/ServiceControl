// ReSharper disable MemberCanBePrivate.Global
namespace ServiceControlInstaller.Engine.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Management;
    using System.Security;
    using System.ServiceProcess;
    using System.Text.RegularExpressions;
    using Microsoft.Win32;
    using ServiceControlInstaller.Engine.Accounts;
    using ServiceControlInstaller.Engine.Api;

    [System.ComponentModel.DesignerCategory("Code")]  //Stop the stupid design-time component view being inherited
    public class WindowsServiceController : ServiceController
    {
        public WindowsServiceController(string serviceName, string exePath) : base(serviceName)
        {
            ExePath = exePath;
        }

        public string ExePath { get; }

        public string Description
        {
            get
            {
                return (string) ReadValue("Description");
            }
            set
            {
                WriteValue("Description", value ?? string.Empty);
            }
        }

        public void SetStartupMode(string startMode)
        {
            var validModes = new[] { "Automatic", "Manual", "Disabled" };
            if (!validModes.Any(p => p.Equals(startMode, StringComparison.OrdinalIgnoreCase)))
            {
                throw new ArgumentException($"Invalid startmode:'{startMode}'. Valid options are: {string.Join(", ", validModes)}");
            }

            // ReSharper disable once StringLiteralTypo
            using (var classInstance = new ManagementObject(@"\\.\root\cimv2", $"Win32_Service.Name='{ServiceName}'", null))
            using (var inParams = classInstance.GetMethodParameters("ChangeStartMode"))
            {
                inParams["StartMode"] = startMode;
                using (var outParams = classInstance.InvokeMethod("ChangeStartMode", inParams, null))
                {
                    if (outParams == null)
                    {
                        throw new ManagementException($"Failed to set service to {startMode}");
                    }
                    var wmiReturnCode = Convert.ToInt32(outParams["ReturnValue"]);
                    if (wmiReturnCode != 0)
                    {
                        throw new ManagementException($"Failed to set service to {startMode} - {Win32ServiceErrorMessages[wmiReturnCode]}");
                    }
                }
            }
        }

        public string Account
        {
            get
            {
                var account = (string) ReadValue("ObjectName");
                if (account == null)
                {
                    throw new NullReferenceException("ServiceAccount entry not found in the registry");
                }
                return account;
            }
        }

        public void ChangeAccountDetails(string serviceAccount, string servicePassword)
        {
            var userAccount = UserAccount.ParseAccountName(serviceAccount);

            if (!(userAccount.IsLocalService() || userAccount.IsLocalService()))
            {
                var privileges = Lsa.GetPrivileges(userAccount.QualifiedName).ToList();
                if (!privileges.Contains(LogonPrivileges.LogonAsAService, StringComparer.OrdinalIgnoreCase))
                {
                    privileges.Add(LogonPrivileges.LogonAsAService);
                    Lsa.GrantPrivileges(userAccount.QualifiedName, privileges.ToArray());
                }
            }

            var  objPath = $"Win32_Service.Name='{ServiceName}'";
            using (var win32Service = new ManagementObject(new ManagementPath(objPath)))
            {
                var inParams = win32Service.GetMethodParameters("Change");
                inParams["StartName"] = ConvertAccountNameToServiceAccount(userAccount.QualifiedName);
                inParams["StartPassword"] = servicePassword;

                var outParams = win32Service.InvokeMethod("Change", inParams, null);
                if (outParams == null)
                {
                    throw new ManagementException($"Failed to set account credentials service {ServiceName}");
                }

                var wmiReturnCode = Convert.ToInt32(outParams["ReturnValue"]);
                if (wmiReturnCode != 0)
                {
                    var message = wmiReturnCode < Win32ChangeErrorMessages.Length
                        ? $"Failed to change service credentials on service {ServiceName} - {Win32ChangeErrorMessages[wmiReturnCode]}"
                        : "An unknown error occurred";

                    if (wmiReturnCode == 22)
                    {
                        message += $"( AccountName {userAccount.QualifiedName} converted to {ConvertAccountNameToServiceAccount(userAccount.QualifiedName)})";
                    }

                    throw new ManagementException(message);
                }
            }
        }

        public static void RegisterNewService(WindowsServiceDetails serviceInfo, params string[] serviceDependencies)
        {
            using (var win32Service = new ManagementClass("Win32_Service"))
            using (var inParams = win32Service.GetMethodParameters("Create"))
            {
                inParams["Name"] = serviceInfo.Name;
                inParams["DisplayName"] = serviceInfo.DisplayName;
                inParams["PathName"] = serviceInfo.ImagePath;
                inParams["ServiceType"] = 16; // Own Process
                inParams["ErrorControl"] = 1; //Report to user
                inParams["StartMode"] = "Automatic";
                inParams["DesktopInteract"] = false;
                inParams["StartName"] = ConvertAccountNameToServiceAccount(serviceInfo.ServiceAccount);
                inParams["StartPassword"] = serviceInfo.ServiceAccountPwd;
                inParams["LoadOrderGroup"] = null;
                inParams["LoadOrderGroupDependencies"] = null;
                inParams["ServiceDependencies"] = serviceDependencies;

                var outParams = win32Service.InvokeMethod("create", inParams, null);
                if (outParams == null)
                {
                    throw new ManagementException($"Failed to create service {serviceInfo.Name}");
                }
                var wmiReturnCode = Convert.ToInt32(outParams["ReturnValue"]);
                if (wmiReturnCode != 0)
                {
                    var message = wmiReturnCode < Win32ChangeErrorMessages.Length
                        ? $"Failed to create service to {serviceInfo.Name} - {Win32ServiceErrorMessages[wmiReturnCode]}"
                        : "An unknown error occurred";

                    if (wmiReturnCode == 22)
                    {
                        message += $"( AccountName {serviceInfo.ServiceAccount} converted to {ConvertAccountNameToServiceAccount(serviceInfo.ServiceAccount)})";
                    }


                    throw new ManagementException(message);
                }

                if (!string.IsNullOrWhiteSpace(serviceInfo.ServiceDescription))
                {
                    using (var servicesBaseKey = Registry.LocalMachine.OpenSubKey(@"System\CurrentControlSet\Services"))
                    {
                        if (servicesBaseKey != null)
                        {
                            using (var serviceKey = servicesBaseKey.OpenSubKey(serviceInfo.Name, true))
                            {
                                serviceKey?.SetValue("Description", serviceInfo.ServiceDescription);
                            }
                        }
                    }
                }
                ServiceRecoveryHelper.SetRecoveryOptions(serviceInfo.Name);
            }
        }

        /// When Local System or LocalService  are the service account you must pass these non localized name to the service creation/modification
        static string ConvertAccountNameToServiceAccount(string account)
        {
            var userAccount = UserAccount.ParseAccountName(account);
            if (userAccount.IsLocalSystem())
            {
                return @".\LOCALSYSTEM";
            }
            if (userAccount.IsLocalService())
            {
               return @"NT AUTHORITY\LOCALSERVICE";
            }

            return userAccount.QualifiedName;
        }

        public void Delete()
        {
            // ReSharper disable once StringLiteralTypo
            using (var classInstance = new ManagementObject(@"\\.\root\cimv2", $"Win32_Service.Name='{ServiceName}'", null))
            using (var outParams = classInstance.InvokeMethod("Delete", null, null))
            {
                if (outParams == null || Convert.ToInt32(outParams["ReturnValue"]) != 0)
                {
                    throw new ManagementException($"Failed to delete service to {ServiceName}");
                }
            }
        }

        object ReadValue(string key)
        {
            using (var servicesBaseKey = Registry.LocalMachine.OpenSubKey(@"System\CurrentControlSet\Services"))
            {
                if (servicesBaseKey != null)
                {
                    using (var serviceKey = servicesBaseKey.OpenSubKey(ServiceName))
                    {
                        if (serviceKey != null)
                        {
                            return serviceKey.GetValue(key, null);
                        }
                    }
                }
            }
            return null;
        }

        void WriteValue(string name, string value)
        {
            using (var servicesBaseKey = Registry.LocalMachine.OpenSubKey(@"System\CurrentControlSet\Services"))
            {
                if (servicesBaseKey != null)
                {
                    using (var serviceKey = servicesBaseKey.OpenSubKey(ServiceName, true))
                    {
                        serviceKey?.SetValue(name, value);
                    }
                }
            }
        }


        public static IEnumerable<WindowsServiceController> FindInstancesByExe(string exename)
        {
            var imagePathRegex = new Regex("^\"{0,1}(?<PATH>.+" + Regex.Escape(exename) + ")\"{0,1}", RegexOptions.IgnoreCase);
            using (var servicesBaseKey = Registry.LocalMachine.OpenSubKey(@"System\CurrentControlSet\Services"))
                if (servicesBaseKey != null)
                {
                    foreach (var serviceName in servicesBaseKey.GetSubKeyNames())
                    {
                        RegistryKey serviceKey = null;
                        try
                        {
                            try
                            {
                                serviceKey = servicesBaseKey.OpenSubKey(serviceName);
                                if (serviceKey == null)
                                    continue;
                            }
                            catch(SecurityException)
                            {
                               continue;
                            }

                            var entryType = (int) serviceKey.GetValue("Type", 0);
                            if (entryType == 1) // driver not a service
                                continue;

                            var imagePath = serviceKey.GetValue("ImagePath", null) as string;
                            if (imagePath == null)
                                continue;

                            var match = imagePathRegex.Match(imagePath);
                            if (match.Success)
                            {
                                imagePath = match.Groups["PATH"].Value;
                                yield return new WindowsServiceController(serviceName, imagePath);
                            }
                        }
                        finally
                        {
                            serviceKey?.Dispose();
                        }
                    }
                }
        }


        static readonly string[] Win32ChangeErrorMessages = {
                                    "The request was accepted",
                                    "The request is not supported",
                                    "Access Denied",
                                    "The service cannot be stopped because other services that are running are dependent on it",
                                    "The requested control code is not valid, or it is unacceptable to the service",
                                    "The requested control code cannot be sent to the service because the of the service state",
                                    "The service has not been started",
                                    "The service did not respond to the start request in a timely fashion",
                                    "Unknown failure when starting the service",
                                    "The directory path to the service executable file was not found",
                                    "The service is already running",
                                    "The database to add a new service is locked",
                                    "A dependency this service relies on has been removed from the system",
                                    "The service failed to find the service needed from a dependent service",
                                    "The service has been disabled from the system",
                                    "The service does not have the correct authentication to run on the system",
                                    "This service is being removed from the system",
                                    "The service has no execution thread",
                                    "The service has circular dependencies when it starts",
                                    "A service is running under the same name",
                                    "The service name has invalid characters",
                                    "Invalid parameters have been passed to the service",
                                    "The account under which this service runs is either invalid or lacks the permissions to run the service",
                                    "The service exists in the database of services available from the system",
                                    "The service is currently paused in the system"
        };

        static readonly string[] Win32ServiceErrorMessages = {
                                    "The request was accepted.",
                                    "The request is not supported.",
                                    "The user did not have the necessary access.",
                                    "The service cannot be stopped because other services that are running are dependent on it.",
                                    "The requested control code is not valid, or it is unacceptable to the service.",
                                    "The requested control code cannot be sent to the service because the state of the service (Win32_BaseService State property) is equal to 0, 1, or 2.",
                                    "The service has not been started.",
                                    "The service did not respond to the start request in a timely fashion.",
                                    "Unknown failure when starting the service.",
                                    "The directory path to the service executable file was not found.",
                                    "The service is already running.",
                                    "The database to add a new service is locked.",
                                    "A dependency this service relies on has been removed from the system.",
                                    "The service failed to find the service needed from a dependent service.",
                                    "The service has been disabled from the system.",
                                    "The service does not have the correct authentication to run on the system.",
                                    "This service is being removed from the system.",
                                    "The service has no execution thread.",
                                    "The service has circular dependencies when it starts.",
                                    "A service is running under the same name.",
                                    "The service name has invalid characters.",
                                    "Invalid parameters have been passed to the service.",
                                    "The account under which this service runs is either invalid or lacks the permissions to run the service.",
                                    "The service exists in the database of services available from the system.",
                                    "The service is currently paused in the system."
                                };
    }
}