namespace ServiceControl.Audit
{
    using System;
    using System.IO;
    using System.Reflection;
    using System.Threading.Tasks;
    using Infrastructure;
    using Infrastructure.Hosting;
    using Infrastructure.Hosting.Commands;
    using Infrastructure.Settings;
    using NServiceBus.Logging;
    using ServiceControl.Audit.Persistence;
    using ServiceControl.Transports;

    class Program
    {
        static Settings settings;

        static async Task Main(string[] args)
        {
            AppDomain.CurrentDomain.AssemblyResolve += (s, e) => ResolveAssembly(e.Name);
            AppDomain.CurrentDomain.UnhandledException += (s, e) => LogException(e.ExceptionObject as Exception);

            var arguments = new HostArguments(args);

            if (arguments.Help)
            {
                arguments.PrintUsage();
                return;
            }

            var loggingSettings = new LoggingSettings(arguments.ServiceName, logToConsole: !arguments.RunAsWindowsService);
            LoggingConfigurator.ConfigureLogging(loggingSettings);

            settings = Settings.FromConfiguration(arguments.ServiceName);

            await new CommandRunner(arguments.Commands).Execute(arguments, settings);
        }

        static void LogException(Exception ex)
        {
            Logger.Error("Unhandled exception was caught.", ex);
        }

        static Assembly ResolveAssembly(string name)
        {
            var assemblyLocation = Assembly.GetEntryAssembly().Location;
            var appDirectory = Path.GetDirectoryName(assemblyLocation);
            var requestingName = new AssemblyName(name).Name;

            var combine = Path.Combine(appDirectory, requestingName + ".dll");
            var assembly = !File.Exists(combine) ? null : Assembly.LoadFrom(combine);

            if (assembly == null && settings != null)
            {
                var transportFolder = TransportManifestLibrary.GetTransportFolder(settings.TransportType);
                assembly = TryLoadAssembly(transportFolder, requestingName);
            }

            if (assembly == null && settings != null)
            {
                var persistenceFolder = PersistenceManifestLibrary.GetPersistenceFolder(settings.PersistenceType);
                assembly = TryLoadAssembly(persistenceFolder, requestingName);
            }

            return assembly;
        }

        static Assembly TryLoadAssembly(string folderPath, string requestingName)
        {
            if (folderPath != null)
            {
                var path = Path.Combine(folderPath, $"{requestingName}.dll");

                if (File.Exists(path))
                {
                    return Assembly.LoadFrom(path);
                }
            }

            return null;
        }

        static readonly ILog Logger = LogManager.GetLogger(typeof(Program));
    }
}
