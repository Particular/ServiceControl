namespace ServiceControl.Audit
{
    using System;
    using System.Configuration;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.Loader;
    using System.Threading.Tasks;
    using Infrastructure;
    using Infrastructure.Hosting;
    using Infrastructure.Hosting.Commands;
    using Infrastructure.Settings;
    using Microsoft.Extensions.Hosting.WindowsServices;
    using NServiceBus.Logging;
    using ServiceControl.Audit.Persistence;
    using ServiceControl.Transports;

    class Program
    {
        static Settings settings;

        static async Task Main(string[] args)
        {
            AssemblyLoadContext.Default.Resolving += ResolveAssembly;
            AppDomain.CurrentDomain.UnhandledException += (s, e) => Logger.Error("Unhandled exception was caught.", e.ExceptionObject as Exception);

            ReadExeConfiguration();

            var arguments = new HostArguments(args);

            if (arguments.Help)
            {
                arguments.PrintUsage();
                return;
            }

            var loggingSettings = new LoggingSettings(arguments.ServiceName, logToConsole: !WindowsServiceHelpers.IsWindowsService());
            LoggingConfigurator.ConfigureLogging(loggingSettings);

            settings = Settings.FromConfiguration(arguments.ServiceName);

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

        static void ReadExeConfiguration()
        {
            // ConfigurationManager on .NET is looking for {assembly}.dll.config files, but all previous versions of ServiceControl will have {assembly}.exe.config instead.
            // This code reads in the exe.config files and adds all the values into the ConfigurationManager's collections.

            var assembly = Assembly.GetExecutingAssembly();
            var location = Path.GetDirectoryName(assembly.Location);
            var assemblyName = assembly.GetName();
            var exePath = Path.Combine(location, $"{assemblyName.Name}.exe");
            var configuration = ConfigurationManager.OpenExeConfiguration(exePath);

            foreach (var key in configuration.AppSettings.Settings.AllKeys)
            {
                ConfigurationManager.AppSettings.Set(key, configuration.AppSettings.Settings[key].Value);
            }

            // This reflection is required because the connection strings collection has had its read only flag set, so it won't let anything be added to it.
            var type = typeof(ConfigurationElementCollection);
            var field = type.GetField("_readOnly", BindingFlags.NonPublic | BindingFlags.Instance);
            field?.SetValue(ConfigurationManager.ConnectionStrings, false);

            foreach (var connectionStringSetting in configuration.ConnectionStrings.ConnectionStrings.Cast<ConnectionStringSettings>())
            {
                ConfigurationManager.ConnectionStrings.Add(connectionStringSetting);
            }
        }

        static readonly ILog Logger = LogManager.GetLogger(typeof(Program));
    }
}
