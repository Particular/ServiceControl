using System;
using System.Reflection;
using NServiceBus.Logging;
using ServiceControl.Audit.Infrastructure.Hosting;
using ServiceControl.Audit.Infrastructure.Hosting.Commands;
using ServiceControl.Audit.Infrastructure.Settings;
using ServiceControl.Configuration;
using ServiceControl.Infrastructure;

AppDomain.CurrentDomain.UnhandledException += (s, e) => LogManager.GetLogger(typeof(Program)).Error("Unhandled exception was caught.", e.ExceptionObject as Exception);

ExeConfiguration.PopulateAppSettings(Assembly.GetExecutingAssembly());

var arguments = new HostArguments(args);

if (arguments.Help)
{
    arguments.PrintUsage();
    return;
}

var loggingSettings = new LoggingSettings(Settings.SettingsRootNamespace);
LoggingConfigurator.ConfigureLogging(loggingSettings);

var settings = new Settings(loggingSettings: loggingSettings);

await new CommandRunner(arguments.Command).Execute(arguments, settings);
