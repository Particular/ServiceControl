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
            commands = new List<Type> { typeof(RunCommand) };
            ServiceName = "Particular.ServiceControl";
            ServiceAccount = ServiceAccount.LocalSystem;
            Username = String.Empty;

            defaultOptions = new OptionSet
            {
                {
                    "?|h|help", "Help about the command line options.", key => { Help = true; }
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
                                                            commands = new List<Type>
                                                                       {
                                                                           typeof(MaintCommand)
                                                                       };
                                                            executionMode = ExecutionMode.Maintenance;
                                                   }
                                               }
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
                        commands = new List<Type>
                        {
                            typeof(RunBootstrapperAndNServiceBusInstallers),
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
                    "user",
                    @"Run the service with the specified username and password. Alternative the system will prompt for a valid username and password if values for both the username and password are not specified."
                    , s => { ServiceAccount = ServiceAccount.User; }
                },
            };

            try
            {
                installOptions.Parse(args);
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

        public List<Type> Commands
        {
            get { return commands; }
        }

        public Dictionary<string, string> Options
        {
            get { return options; }
        } 

        public bool Help { get; set; }
        public string ServiceName { get; set; }
        public string Username { get; set; }
        public ServiceAccount ServiceAccount { get; set; }

        public void PrintUsage()
        {
            var sb = new StringBuilder();

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

            installOptions.WriteOptionDescriptions(new StringWriter(sb));
            var installOptionsHelp = sb.ToString();

            sb.Clear();
            defaultOptions.WriteOptionDescriptions(new StringWriter(sb));
            var defaultOptionsHelp = sb.ToString();

            Console.Out.WriteLine(helpText, defaultOptionsHelp, installOptionsHelp);
        }

        readonly OptionSet installOptions;
        readonly OptionSet defaultOptions;
        List<Type> commands;
        Dictionary<string, string> options = new Dictionary<string, string>();
    }

    internal enum ExecutionMode
    {
        Install,
        Run,
        Maintenance
    }
}