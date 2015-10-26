namespace Particular.ServiceControl.Hosting
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;
    using System.ServiceProcess;
    using System.Text;
    using Commands;
    using global::ServiceControl.Hosting.Commands;
    using ServiceBus.Management.Infrastructure.Settings;

    public class HostArguments
    {
        public HostArguments(string[] args)
        {
            var executionMode = ExecutionMode.Run;
            Commands = new List<Type> { typeof(RunCommand) };
            StartMode = StartMode.Automatic;
            ServiceName = "Particular.ServiceControl";
            DisplayName = "Particular ServiceControl";
            Description = "Particular Software ServiceControl for NServiceBus.";
            ServiceAccount = ServiceAccount.LocalSystem;
            Username = String.Empty;
            Password = String.Empty;

            var defaultOptions = new OptionSet
            {
                {
                    "?|h|help", "Help about the command line options.", key => { Help = true; }
                },
            };

            var maintenanceOptions = new OptionSet
                                           {
                                               {
                                                   "m|maint|maintenance",
                                                   @"Run RavenDB only - use for DB maintenance", 
                                                   s => {
                                                            Commands = new List<Type>
                                                                       {
                                                                           typeof(MaintCommand)
                                                                       };
                                                            executionMode = ExecutionMode.Maintenance;
                                                   }
                                               }
                                           };

            // Not documented in help - Used bt SC installer only
            var externalInstallerOptions = new OptionSet
            {
                {
                    "s|setup",
                    @"Internal use - for new installer"
                    , s =>
                    {

                        Commands = new List<Type>{typeof(RunBootstrapperAndNServiceBusInstallers)};
                        executionMode = ExecutionMode.Install;
                    }
                },
                {
                    "serviceName=",
                    @"Specify the service name for the installed service."
                    , s => { ServiceName = s; }
                },
                {
                    "userName=",
                    @"Username for the account the service should run under."
                    , s => { Username = s; }
                },
            };
            
            try
            {
                externalInstallerOptions.Parse(args);
                if (executionMode == ExecutionMode.Install)
                {
                    return;
                }
                
                maintenanceOptions.Parse(args);
                if (executionMode == ExecutionMode.Maintenance)
                {
                    Settings.MaintenanceMode = true;
                    return;
                }

                defaultOptions.Parse(args);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Help = true;
            }
        }

        public List<Type> Commands { get; private set; }

        public Dictionary<string, string> Options
        {
            get { return options; }
        } 

        public bool Help { get; set; }
        public string ServiceName { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }

        public StartMode StartMode { get; private set; }

        public string Username { get; set; }
        public string Password { get; set; }
        public ServiceAccount ServiceAccount { get; set; }

        public void PrintUsage()
        {
            var helpText = String.Empty;
            using (
                var stream =
                    Assembly.GetCallingAssembly()
                        .GetManifestResourceStream("ServiceControl.Hosting.Help.txt"))
            {
                if (stream != null)
                {
                    using (var streamReader = new StreamReader(stream))
                    {
                        helpText = streamReader.ReadToEnd();
                    }
                }
            }
            
            Console.Out.WriteLine(helpText);
        }

        Dictionary<string, string> options = new Dictionary<string, string>();
    }

    internal enum ExecutionMode
    {
        Install,
        Uninstall,
        Run,
        Maintenance
    }

    public enum StartMode
    {
        Manual,
        Automatic,
        Delay,
        Disabled
    }
}