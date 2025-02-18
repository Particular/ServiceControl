using System;
using System.Reflection;
using NServiceBus.Logging;
using Particular.ServiceControl.Hosting;
using ServiceBus.Management.Infrastructure.Settings;
using ServiceControl.Configuration;
using ServiceControl.Hosting.Commands;
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
finally
{
    // Leave a trail in the logs to determine if the process was killed
    NLog.LogManager.GetCurrentClassLogger().Info("Done!");
    NLog.LogManager.Shutdown();
}