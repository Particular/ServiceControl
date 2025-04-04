﻿using System;
using System.Reflection;
using NServiceBus.Logging;
using ServiceControl.Audit.Infrastructure.Hosting;
using ServiceControl.Audit.Infrastructure.Hosting.Commands;
using ServiceControl.Audit.Infrastructure.Settings;
using ServiceControl.Configuration;
using ServiceControl.Infrastructure;

try
{
    AppDomain.CurrentDomain.UnhandledException += (s, e) => LogManager.GetLogger(typeof(Program)).Error("Unhandled exception was caught.", e.ExceptionObject as Exception);

    // Hack: See https://github.com/Particular/ServiceControl/issues/4392
    var exitCode = await IntegratedSetup.Run();

    if (exitCode != 0)
    {
        return exitCode;
    }

    ExeConfiguration.PopulateAppSettings(Assembly.GetExecutingAssembly());

    var arguments = new HostArguments(args);

    if (arguments.Help)
    {
        arguments.PrintUsage();
        return 0;
    }

    var loggingSettings = new LoggingSettings(Settings.SettingsRootNamespace);
    LoggingConfigurator.ConfigureLogging(loggingSettings);

    var settings = new Settings(loggingSettings: loggingSettings);

    await new CommandRunner(arguments.Command).Execute(arguments, settings);

    return 0;
}
catch (Exception ex)
{
    NLog.LogManager.GetCurrentClassLogger().Fatal(ex, "Unrecoverable error");
    throw;
}
finally
{
    // The following log statement is meant to leave a trail in the logs to determine if the process was killed
    NLog.LogManager.GetCurrentClassLogger().Info("Shutdown complete");
    NLog.LogManager.Shutdown();
}