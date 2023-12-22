namespace Particular.ServiceControl
{
    using System;
    using System.IO;
    using System.Reflection;
    using System.Runtime.Loader;
    using System.Threading.Tasks;
    using Commands;
    using global::ServiceControl.Persistence;
    using global::ServiceControl.Transports;
    using Hosting;
    using NServiceBus.Logging;
    using ServiceBus.Management.Infrastructure.Settings;

    class Program
    {
        static Settings settings;

        static async Task Main(string[] args)
        {
            AssemblyLoadContext.Default.Resolving += ResolveAssembly;
            AppDomain.CurrentDomain.UnhandledException += (s, e) => Logger.Error("Unhandled exception was caught.", e.ExceptionObject as Exception);

            var arguments = new HostArguments(args);

            if (arguments.Help)
            {
                arguments.PrintUsage();
                return;
            }

            var loggingSettings = new LoggingSettings(arguments.ServiceName, logToConsole: !arguments.RunAsWindowsService);
            LoggingConfigurator.ConfigureLogging(loggingSettings);

            settings = new Settings(arguments.ServiceName);

            await new CommandRunner(arguments.Commands).Execute(arguments, settings);
        }

        static Assembly ResolveAssembly(AssemblyLoadContext loadContext, AssemblyName assemblyName)
        {
            if (settings == null)
            {
                return null;
            }

            var transportFolder = TransportManifestLibrary.GetTransportFolder(settings.TransportType);
            var assembly = TryLoadAssembly(loadContext, transportFolder, assemblyName);

            if (assembly == null)
            {
                var persistenceFolder = PersistenceManifestLibrary.GetPersistenceFolder(settings.PersistenceType);
                assembly = TryLoadAssembly(loadContext, persistenceFolder, assemblyName);
            }

            return assembly;
        }

        static Assembly TryLoadAssembly(AssemblyLoadContext loadContext, string folderPath, AssemblyName assemblyName)
        {
            if (folderPath != null)
            {
                var path = Path.Combine(folderPath, $"{assemblyName.Name}.dll");

                if (File.Exists(path))
                {
                    return loadContext.LoadFromAssemblyPath(path);
                }
            }

            return null;
        }

        static readonly ILog Logger = LogManager.GetLogger(typeof(Program));
    }
}