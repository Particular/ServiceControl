using System;
using System.Reflection;
using NServiceBus.Logging;
using ServiceControl.Configuration;
using ServiceControl.Monitoring;

AppDomain.CurrentDomain.UnhandledException += (s, e) => LogManager.GetLogger(typeof(Program)).Error("Unhandled exception was caught.", e.ExceptionObject as Exception);

ExeConfiguration.PopulateAppSettings(Assembly.GetExecutingAssembly());

var arguments = new HostArguments(args);

var loggingSettings = new LoggingSettings();
LoggingConfigurator.ConfigureLogging(loggingSettings);

var settings = new Settings(loggingSettings);
arguments.ApplyOverridesTo(settings);

await new CommandRunner(arguments.Commands).Execute(settings);
