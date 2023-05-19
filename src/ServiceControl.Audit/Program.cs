﻿namespace ServiceControl.Audit
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;
    using Infrastructure;
    using Infrastructure.Hosting;
    using Infrastructure.Hosting.Commands;
    using Infrastructure.Settings;
    using NServiceBus.Logging;

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
            if (assembly == null && !string.IsNullOrWhiteSpace(settings.TransportName))
            {
                //We are only interested in the directory that is the first segment
                var transportNameFolder = settings.TransportName.Split('.').First();
                var subFolderPath = Path.Combine(appDirectory, "Transports", transportNameFolder);
                assembly = TryLoadTypeFromSubdirectory(subFolderPath, requestingName);
            }

            if (assembly == null && !string.IsNullOrWhiteSpace(settings.PersistenceName))
            {
                //We are only interested in the directory
                var persisterNameFolder = settings.PersistenceName.Split('.').First();
                var subFolderPath = Path.Combine(appDirectory, "Persisters", persisterNameFolder);
                assembly = TryLoadTypeFromSubdirectory(subFolderPath, requestingName);
            }

            return assembly;
        }

        static Assembly TryLoadTypeFromSubdirectory(string subFolderPath, string requestingName)
        {
            //look into any subdirectory
            var file = Directory.EnumerateFiles(subFolderPath, requestingName + ".dll", SearchOption.AllDirectories).SingleOrDefault();
            if (file != null)
            {
                return Assembly.LoadFrom(file);
            }

            return null;
        }

        static readonly ILog Logger = LogManager.GetLogger(typeof(Program));
    }
}