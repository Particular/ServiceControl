namespace ServiceControl.Monitoring
{
    using System;
    using System.IO;
    using System.Reflection;
    using System.Runtime.Loader;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Hosting.WindowsServices;
    using NServiceBus.Logging;
    using ServiceControl.Configuration;
    using ServiceControl.Transports;

    static class Program
    {
        static Settings settings;

        static async Task Main(string[] args)
        {
            AssemblyLoadContext.Default.Resolving += ResolveAssembly;
            AppDomain.CurrentDomain.UnhandledException += (s, e) => Logger.Error("Unhandled exception was caught.", e.ExceptionObject as Exception);

            ExeConfiguration.PopulateAppSettings(Assembly.GetExecutingAssembly());

            var arguments = new HostArguments(args);

            LoadSettings(arguments);

            LoggingConfigurator.Configure(settings, !WindowsServiceHelpers.IsWindowsService());

            await new CommandRunner(arguments.Commands)
                .Run(settings);
        }

        static void LoadSettings(HostArguments args)
        {
            var _settings = new Settings();
            args.ApplyOverridesTo(_settings);
            settings = _settings;
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

        static readonly ILog Logger = LogManager.GetLogger(typeof(Program));
    }
}