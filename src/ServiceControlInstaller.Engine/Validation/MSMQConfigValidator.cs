namespace ServiceControlInstaller.Engine.Validation
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.ServiceProcess;
    using Microsoft.Win32;

    class MsmqConfigValidator
    {
        public static void Validate()
        {
            CheckServiceIsInstalledAndRunning();
            CheckServiceIsConfiguredCorrectly();
        }

        static void CheckServiceIsInstalledAndRunning()
        {
            var msmqService = ServiceController.GetServices().FirstOrDefault(p => p.ServiceName.Equals("MSMQ", StringComparison.OrdinalIgnoreCase));
            if (msmqService == null)
            {
                throw new EngineValidationException("MSMQ Service is not installed");
            }

            if (msmqService.Status != ServiceControllerStatus.Running)
            {
                throw new EngineValidationException("MSMQ Service is not running");
            }
        }

        static void CheckServiceIsConfiguredCorrectly()
        {
            var undesirableMsmqComponents = new List<MsmqComponent>
            {
                new MsmqComponent
                {
                    Name = "msmq_MQDSServiceInstalled",
                    DisplayName = "MSMQ Directory Services integration"
                },
                new MsmqComponent
                {
                    Name = "msmq_MulticastInstalled",
                    DisplayName = "MSMQ Multicasting Support"
                },
                new MsmqComponent
                {
                    Name = "msmq_RoutingInstalled",
                    DisplayName = "MSMQ Routing"
                },
                new MsmqComponent
                {
                    Name = "msmq_TriggersInstalled",
                    DisplayName = "MSMQ Triggers"
                }
            };

            string[] valueNames;

            var regView = Environment.Is64BitOperatingSystem ? RegistryView.Registry64 : RegistryView.Default;
            using (var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, regView))
            using (var msmqkey = hklm.OpenSubKey(@"SOFTWARE\Microsoft\MSMQ\Setup"))
            {
                if (msmqkey == null)
                {
                    throw new Exception("Error reading the MSMQ configuration from the registry");
                }

                valueNames = msmqkey.GetValueNames();
            }

            var componentsToRemove = undesirableMsmqComponents.Where(undesirableComponent => valueNames.Contains(undesirableComponent.Name, StringComparer.OrdinalIgnoreCase)).Select(p => p.DisplayName).ToArray();
            if (componentsToRemove.Length > 0)
            {
                throw new EngineValidationException($"The MSMQ service has unsupported optional features installed. Please remove the following via control panel or the DISM command line tool,  The unsupported feature(s) are: {string.Join(", ", componentsToRemove)}");
            }
        }

        class MsmqComponent
        {
            public string Name { get; set; }
            public string DisplayName { get; set; }
        }
    }
}