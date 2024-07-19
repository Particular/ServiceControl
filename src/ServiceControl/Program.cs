using System;
using System.Reflection;
using NServiceBus.Logging;
using Particular.ServiceControl.Hosting;
using ServiceBus.Management.Infrastructure.Settings;
using ServiceControl.Configuration;
using ServiceControl.Hosting.Commands;
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
