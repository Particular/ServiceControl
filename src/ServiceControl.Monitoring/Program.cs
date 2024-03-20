namespace ServiceControl.Monitoring
{
    using System;
    using System.IO;
    using System.Reflection;
    using System.Runtime.Loader;
    using System.Threading.Tasks;
    using NServiceBus.Logging;
    using ServiceControl.Configuration;
    using ServiceControl.Transports;

    static class Program
    {
        static Settings settings;

        static async Task Main(string[] args)
        {
            AssemblyLoadContext.Default.Resolving += ResolveAssembly;
            AppDomain.CurrentDomain.UnhandledException += (s, e) => LogManager.GetLogger(typeof(Program)).Error("Unhandled exception was caught.", e.ExceptionObject as Exception);

            ExeConfiguration.PopulateAppSettings(Assembly.GetExecutingAssembly());

            var arguments = new HostArguments(args);

            var loggingSettings = new LoggingSettings();
            LoggingConfigurator.ConfigureLogging(loggingSettings);

            settings = new Settings(loggingSettings);
            arguments.ApplyOverridesTo(settings);

            await new CommandRunner(arguments.Commands).Execute(settings);
        }

        static Assembly ResolveAssembly(AssemblyLoadContext loadContext, AssemblyName assemblyName)
        {
            if (settings == null)
            {
                return null;
            }

            var transportFolder = TransportManifestLibrary.GetTransportFolder(settings.TransportType);
            var assembly = TryLoadAssembly(loadContext, transportFolder, assemblyName);

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