namespace ServiceControl.Audit
{
    using System;
    using System.IO;
    using System.Reflection;
    using System.Runtime.Loader;
    using System.Threading.Tasks;
    using Infrastructure;
    using Infrastructure.Hosting;
    using Infrastructure.Hosting.Commands;
    using Infrastructure.Settings;
    using NServiceBus.Logging;
    using ServiceControl.Audit.Persistence;
    using ServiceControl.Configuration;
    using ServiceControl.Transports;

    class Program
    {
        static Settings settings;

        static async Task Main(string[] args)
        {
            AssemblyLoadContext.Default.Resolving += ResolveAssembly;
            AppDomain.CurrentDomain.UnhandledException += (s, e) => LogManager.GetLogger(typeof(Program)).Error("Unhandled exception was caught.", e.ExceptionObject as Exception);

            ExeConfiguration.PopulateAppSettings(Assembly.GetExecutingAssembly());

            var arguments = new HostArguments(args);

            if (arguments.Help)
            {
                arguments.PrintUsage();
                return;
            }

            var loggingSettings = new LoggingSettings();
            LoggingConfigurator.ConfigureLogging(loggingSettings);

            settings = new Settings(arguments.ServiceName, loggingSettings: loggingSettings);

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
    }
}
