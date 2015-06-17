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

            defaultOptions = new OptionSet
            {
                {
                    "?|h|help", "Help about the command line options.", key => { Help = true; }
                },
                {
                    "d|set={==}", "The configuration {0:option} to set to the specified {1:value}", (key, value) =>
                    {
                        options[key] = value;

                        Commands = new List<Type>
                        {
                            typeof(WriteOptionsCommand),
                        };
                    }
                },
                {
                    "restart",
                    @"Restarts the endpoint."
                    , s =>
                    {
                        Commands = new List<Type>
                        {
                            typeof(WriteOptionsCommand),
                            typeof(RestartCommand),
                        };
                    }
                },
                {
                    "start",
                    @"Starts the endpoint."
                    , s =>
                    {
                        Commands = new List<Type>
                        {
                            typeof(WriteOptionsCommand),
                            typeof(StartCommand),
                        };
                    }
                },
                {
                    "stop",
                    @"Stops the endpoint."
                    , s =>
                    {
                        Commands = new List<Type>
                        {
                            typeof(StopCommand),
                        };
                    }
                },
                {
                    "serviceName=",
                    @"Specify the service name for the installed service."
                    , s => { ServiceName = s; }
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

         
            uninstallOptions = new OptionSet
            {
                {
                    "?|h|help", "Help about the command line options.", key => { Help = true; }
                },
                {
                    "u|uninstall",
                    @"Uninstall the endpoint as a Windows service."
                    , s =>
                    {
                        Commands = new List<Type> {typeof(UninstallCommand)};
                        executionMode = ExecutionMode.Uninstall;
                    }
                },
                {
                    "serviceName=",
                    @"Specify the service name for the installed service."
                    , s => { ServiceName = s; }
                }
            };
            
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


            installOptions = new OptionSet
            {
                {
                    "?|h|help",
                    "Help about the command line options.",
                    key => { Help = true; }
                },
                {
                    "i|install",
                    @"Install the endpoint as a Windows service."
                    , s =>
                    {
                        Commands = new List<Type>
                        {
                            typeof(WriteOptionsCommand),
                            typeof(CheckMandatoryInstallOptionsCommand),
                            typeof(RunBootstrapperAndNServiceBusInstallers),
                            typeof(InstallCommand)
                        };
                        executionMode = ExecutionMode.Install;
                    }
                },
                {
                    "serviceName=",
                    @"Specify the service name for the installed service."
                    , s => { ServiceName = s; }
                },
                {
                    "displayName=",
                    @"Friendly name for the installed service."
                    , s => { DisplayName = s; }
                },
                {
                    "description=",
                    @"Description for the service."
                    , s => { Description = s; }
                },
                {
                    "username=",
                    @"Username for the account the service should run under."
                    , s => { Username = s; }
                },
                {
                    "password=",
                    @"Password for the service account."
                    , s => { Password = s; }
                },
                {
                    "localservice",
                    @"Run the service with the local service account."
                    , s => { ServiceAccount = ServiceAccount.LocalService; }
                },
                {
                    "networkservice",
                    @"Run the service with the network service permission."
                    , s => { ServiceAccount = ServiceAccount.NetworkService; }
                },
                {
                    "user",
                    @"Run the service with the specified username and password. Alternative the system will prompt for a valid username and password if values for both the username and password are not specified."
                    , s => { ServiceAccount = ServiceAccount.User; }
                },
                {
                    "delayed",
                    @"The service should start automatically (delayed)."
                    , s => { StartMode = StartMode.Delay; }
                },
                {
                    "autostart",
                    @"The service should start automatically (default)."
                    , s => { StartMode = StartMode.Automatic; }
                },
                {
                    "disabled",
                    @"The service should be set to disabled."
                    , s => { StartMode = StartMode.Disabled; }
                },
                {
                    "manual",
                    @"The service should be started manually."
                    , s => { StartMode = StartMode.Manual; }
                },
                {
                    "d|set={==}", "The configuration {0:option} to set to the specified {1:value}", (key, value) =>
                    {
                        options[key] = value;
                    }
                },
            };

            try
            {
                installOptions.Parse(args);
                if (executionMode == ExecutionMode.Install)
                {
                    return;
                }

                externalInstallerOptions.Parse(args);
                if (executionMode == ExecutionMode.Install)
                {
                    return;
                }

                uninstallOptions.Parse(args);
                if (executionMode == ExecutionMode.Uninstall)
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
        public string OutputPath { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public ServiceAccount ServiceAccount { get; set; }

        public void PrintUsage()
        {
            var sb = new StringBuilder();
            var helpText = String.Empty;
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("ServiceControl.Shell.Hosting.Help.txt"))
            {
                if (stream != null)
                {
                    using (var streamReader = new StreamReader(stream))
                    {
                        helpText = streamReader.ReadToEnd();
                    }
                }
            }

            installOptions.WriteOptionDescriptions(new StringWriter(sb));
            var installOptionsHelp = sb.ToString();

            sb.Clear();
            uninstallOptions.WriteOptionDescriptions(new StringWriter(sb));
            var uninstallOptionsHelp = sb.ToString();

            sb.Clear();
            defaultOptions.WriteOptionDescriptions(new StringWriter(sb));
            var defaultOptionsHelp = sb.ToString();

            Console.Out.WriteLine(helpText, defaultOptionsHelp, installOptionsHelp, uninstallOptionsHelp);
        }

        readonly OptionSet installOptions;
        readonly OptionSet uninstallOptions;
        readonly OptionSet defaultOptions;

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