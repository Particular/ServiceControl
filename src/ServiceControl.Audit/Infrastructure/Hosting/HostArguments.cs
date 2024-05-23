namespace ServiceControl.Audit.Infrastructure.Hosting
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using Commands;
    using Configuration;
    using Settings;

    class HostArguments
    {
        public HostArguments(string[] args)
        {
            if (SettingsReader.Read<bool>(Settings.SettingsRootNamespace, "MaintenanceMode"))
            {
                args = args.Concat(new[]
                {
                    "-m"
                }).ToArray();
            }

            var executionMode = ExecutionMode.Run;
            Commands = [typeof(RunCommand)];
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
                        Commands =
                        [
                            typeof(MaintenanceModeCommand)
                        ];
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
                        Commands = [typeof(SetupCommand)];
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

            var reimportFailedAuditsOptions = new OptionSet
            {
                {
                    "import-failed-audits",
                    "Import failed audit messages",
                    s =>
                    {
                        Commands = [typeof(ImportFailedAuditsCommand)];
                        executionMode = ExecutionMode.ImportFailedAudits;
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

                reimportFailedAuditsOptions.Parse(args);
                if (executionMode == ExecutionMode.ImportFailedAudits)
                {
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

        public bool Help { get; set; }

        public string ServiceName { get; set; }

        public string Username { get; set; }

        public bool SkipQueueCreation { get; set; }

        public void PrintUsage()
        {
            var helpText = string.Empty;
            using (
                var stream =
                    Assembly.GetCallingAssembly()
                        .GetManifestResourceStream("ServiceControl.Audit.Infrastructure.Hosting.Help.txt"))
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

    enum ExecutionMode
    {
        RunInstallers,
        Run,
        ImportFailedAudits,
        Maintenance
    }
}