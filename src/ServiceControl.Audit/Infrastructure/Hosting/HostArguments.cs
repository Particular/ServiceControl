namespace ServiceControl.Audit.Infrastructure.Hosting
{
    using System;
    using System.IO;
    using System.Reflection;
    using Commands;
    using Configuration;
    using Settings;

    class HostArguments
    {
        public HostArguments(string[] args, Settings settings)
        {
            if (settings.MaintenanceMode)
            {
                args = [.. args, "-m"];
            }

            var defaultOptions = new OptionSet
            {
                { "?|h|help", "Help about the command line options.", key => Help = true }
            };

            var maintenanceOptions = new OptionSet
            {
                {
                    "m|maint|maintenance",
                    "Run RavenDB only - use for DB maintenance",
                    s => Command = typeof(MaintenanceModeCommand)
                }
            };

            var externalInstallerOptions = new OptionSet
            {
                {
                    "s|setup",
                    "Internal use - for new installer",
                    s => Command = typeof(SetupCommand)
                },
                {
                    "skip-queue-creation",
                    "Skip queue creation during install/update",
                    s => SkipQueueCreation = true
                }
            };

            var reimportFailedAuditsOptions = new OptionSet
            {
                {
                    "import-failed-audits",
                    "Import failed audit messages",
                    s => Command = typeof(ImportFailedAuditsCommand)
                }
            };

            try
            {
                externalInstallerOptions.Parse(args);

                if (Command == typeof(SetupCommand))
                {
                    return;
                }

                maintenanceOptions.Parse(args);

                if (Command == typeof(MaintenanceModeCommand))
                {
                    return;
                }

                reimportFailedAuditsOptions.Parse(args);

                if (Command == typeof(ImportFailedAuditsCommand))
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

        public Type Command { get; private set; } = typeof(RunCommand);

        public bool Help { get; private set; }

        public bool SkipQueueCreation { get; private set; }

        public void PrintUsage()
        {
            var helpText = string.Empty;
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("ServiceControl.Audit.Infrastructure.Hosting.Help.txt");

            if (stream != null)
            {
                using var streamReader = new StreamReader(stream);
                helpText = streamReader.ReadToEnd();
            }

            Console.Out.WriteLine(helpText);
        }
    }
}