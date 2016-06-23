namespace Particular.ServiceControl.Hosting
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;
    using Commands;
    using global::ServiceControl.Hosting.Commands;
    using ServiceBus.Management.Infrastructure.Settings;

    public class HostArguments
    {
        public List<Type> Commands { get; private set; }

        public bool Help { get; set; }
        public string ServiceName { get; set; }
        public string Username { get; set; }
        public bool Portable { get; set; }
        
        public HostArguments(string[] args)
        {
            var executionMode = ExecutionMode.Run;
            Commands = new List<Type> { typeof(RunCommand) };
            ServiceName = "Particular.ServiceControl";
         
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

            // Not documented in help - Used by SC installer only
            var externalInstallerOptions = new OptionSet
            {
                {
                    "s|setup",
                    @"Internal use - for new installer"
                    , s =>
                    {

                        Commands = new List<Type>{typeof(SetupCommand)};
                        executionMode = ExecutionMode.RunInstallers;
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
                }
            };

            var externalUnitTestRunnerOptions = new OptionSet
            {
                {
                    "p|portable", 
                    @"Internal use - runs as a console app, even non-interactively", 
                    s => { Portable = true; }
                }
            };
            
            try
            {
                externalInstallerOptions.Parse(args);
                if (executionMode == ExecutionMode.RunInstallers)
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
                externalUnitTestRunnerOptions.Parse(args);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Help = true;
            }
        }
        
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
    }

    internal enum ExecutionMode
    {
        RunInstallers,
        Run,
        Maintenance
    }
}