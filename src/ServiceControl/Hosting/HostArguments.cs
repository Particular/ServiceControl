namespace Particular.ServiceControl.Hosting
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using Commands;
    using global::ServiceControl.Hosting.Commands;
    using ServiceBus.Management.Infrastructure.Settings;

    class HostArguments
    {
        public HostArguments(string[] args)
        {
            if (ConfigFileSettingsReader<bool>.Read("MaintenanceMode"))
            {
                args = args.Concat(new[]
                {
                    "-m"
                }).ToArray();
            }

            var executionMode = ExecutionMode.Run;
            Commands = new List<Type> {typeof(RunCommand)};
            ServiceName = Settings.DEFAULT_SERVICE_NAME;

            var defaultOptions = new OptionSet
            {
                {
                    "?|h|help", "Help about the command line options.", key => { Help = true; }
                }
            };

            var maintenanceOptions = new OptionSet
            {
                {
                    "m|maint|maintenance",
                    @"Run RavenDB only - use for DB maintenance",
                    s =>
                    {
                        Commands = new List<Type>
                        {
                            typeof(MaintCommand)
                        };
                        executionMode = ExecutionMode.Maintenance;
                    }
                }
            };

            var externalInstallerOptions = new OptionSet
            {
                {
                    "s|setup",
                    @"Internal use - for new installer", s =>
                    {
                        Commands = new List<Type> {typeof(SetupCommand)};
                        executionMode = ExecutionMode.RunInstallers;
                    }
                },
                {
                    "serviceName=",
                    @"Specify the service name for the installed service.", s => { ServiceName = s; }
                },
                {
                    "userName=",
                    @"Username for the account the service should run under.", s => { Username = s; }
                },
                {
                    "skip-queue-creation",
                    @"Skip queue creation during install/update",
                    s => { SkipQueueCreation = true; }
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

            var reimportFailedErrorsOptions = new OptionSet
            {
                {
                    "import-failed-errors",
                    "Import failed error messages",
                    s =>
                    {
                        Commands = new List<Type> {typeof(ImportFailedErrorsCommand)};
                        executionMode = ExecutionMode.ImportFailedErrors;
                    }
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
                    return;
                }

                reimportFailedErrorsOptions.Parse(args);
                if (executionMode == ExecutionMode.ImportFailedErrors)
                {
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

        public List<Type> Commands { get; private set; }

        public bool Help { get; set; }
        public string ServiceName { get; set; }
        public string Username { get; set; }
        public bool Portable { get; set; }
        public bool SkipQueueCreation { get; set; }


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
        Maintenance,
        ImportFailedErrors
    }
}