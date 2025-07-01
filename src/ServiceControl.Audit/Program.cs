using System;
using System.Reflection;
using Microsoft.Extensions.Logging;
using ServiceControl.Audit.Infrastructure.Hosting;
using ServiceControl.Audit.Infrastructure.Hosting.Commands;
using ServiceControl.Audit.Infrastructure.Settings;
using ServiceControl.Configuration;
using ServiceControl.Infrastructure;

ILogger logger = null;

try
{
    var loggingSettings = new LoggingSettings(Settings.SettingsRootNamespace);
    LoggingConfigurator.ConfigureLogging(loggingSettings);
    logger = LoggerUtil.CreateStaticLogger(typeof(Program));

    AppDomain.CurrentDomain.UnhandledException += (s, e) => logger.LogError(e.ExceptionObject as Exception, "Unhandled exception was caught");

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

    var settings = new Settings(loggingSettings: loggingSettings);

    await new CommandRunner(arguments.Command).Execute(arguments, settings);

    return 0;
}
catch (Exception ex)
{
    if (logger != null)
    {
        logger.LogCritical(ex, "Unrecoverable error");
    }
    else
    {
        LoggingConfigurator.ConfigureNLog("bootstrap.${shortdate}.txt", "./", NLog.LogLevel.Fatal);
        NLog.LogManager.GetCurrentClassLogger().Fatal(ex, "Unrecoverable error");
    }
    throw;
}
finally
{
    // The following log statement is meant to leave a trail in the logs to determine if the process was killed
    logger?.LogInformation("Shutdown complete");
    LoggerUtil.DisposeLoggerFactories();
}