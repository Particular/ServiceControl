using System;
using System.Reflection;
using NServiceBus.Logging;
using ServiceControl.Configuration;
using ServiceControl.Infrastructure;
using ServiceControl.Monitoring;

AppDomain.CurrentDomain.UnhandledException += (s, e) => LogManager.GetLogger(typeof(Program)).Error("Unhandled exception was caught.", e.ExceptionObject as Exception);

// Hack: See https://github.com/Particular/ServiceControl/issues/4392
await IntegratedSetup.Run();

ExeConfiguration.PopulateAppSettings(Assembly.GetExecutingAssembly());

var arguments = new HostArguments(args);

var loggingSettings = new LoggingSettings(Settings.SettingsRootNamespace);
LoggingConfigurator.ConfigureLogging(loggingSettings);

var settings = new Settings(loggingSettings: loggingSettings);

await new CommandRunner(arguments.Command).Execute(arguments, settings);
