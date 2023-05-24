namespace Particular.ServiceControl
{
    using System;
    using System.IO;
    using System.Reflection;
    using System.Threading.Tasks;
    using Commands;
    using global::ServiceControl.Transports;
    using Hosting;
    using NServiceBus.Logging;
    using ServiceBus.Management.Infrastructure.Settings;

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

            settings = new Settings(arguments.ServiceName);

            await new CommandRunner(arguments.Commands).Execute(arguments, settings)
                .ConfigureAwait(false);
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
            var transportFolder = TransportManifestLibrary.GetTransportFolder(settings.TransportType);
            if (assembly == null && transportFolder != null)
            {
                var subFolderPath = Path.Combine(appDirectory, "Transports", transportFolder);
                assembly = TryLoadTypeFromSubdirectory(subFolderPath, requestingName);
            }

            return assembly;
        }

        static Assembly TryLoadTypeFromSubdirectory(string subFolderPath, string requestingName)
        {
            var path = Path.Combine(subFolderPath, $"{requestingName}.dll");

            if (File.Exists(path))
            {
                return Assembly.LoadFrom(path);
            }

            return null;
        }

        static readonly ILog Logger = LogManager.GetLogger(typeof(Program));
    }
}