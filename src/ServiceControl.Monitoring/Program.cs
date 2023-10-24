namespace ServiceControl.Monitoring
{
    using System;
    using System.IO;
    using System.Reflection;
    using System.Threading.Tasks;
    using NServiceBus.Logging;
    using ServiceControl.Transports;

    static class Program
    {
        static Settings settings;

        static async Task Main(string[] args)
        {
            AppDomain.CurrentDomain.AssemblyResolve += (s, e) => ResolveAssembly(e.Name);
            AppDomain.CurrentDomain.UnhandledException += (s, e) => LogException(e.ExceptionObject as Exception);

            var arguments = new HostArguments(args);

            LoadSettings(arguments);

            var runAsWindowsService = !Environment.UserInteractive && !arguments.Portable;
            LoggingConfigurator.Configure(settings, !runAsWindowsService);

            await new CommandRunner(arguments.Commands)
                .Run(settings);
        }

        static void LoadSettings(HostArguments args)
        {
            var _settings = new Settings();
            args.ApplyOverridesTo(_settings);
            settings = _settings;
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