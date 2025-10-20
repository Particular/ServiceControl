namespace Particular.ServiceControl.Hosting
{
    using System;
    using System.IO;
    using System.Reflection;
    using global::ServiceControl.Configuration;
    using global::ServiceControl.Hosting.Commands;
    using Particular.LicensingComponent.Shared;
    using ServiceBus.Management.Infrastructure.Settings;

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

            var reimportFailedErrorsOptions = new OptionSet
            {
                {
                    "import-failed-errors",
                    "Import failed error messages",
                    s => Command = typeof(ImportFailedErrorsCommand)
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

                reimportFailedErrorsOptions.Parse(args);

                if (Command == typeof(ImportFailedErrorsCommand))
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

            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("ServiceControl.Hosting.Help.txt");

            if (stream != null)
            {
                using var streamReader = new StreamReader(stream);
                helpText = streamReader.ReadToEnd();
            }

            Console.Out.WriteLine(helpText);
        }
    }
}