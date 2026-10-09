using System;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Particular.ServiceControl.Hosting;
using ServiceBus.Management.Infrastructure.Settings;
using ServiceControl.Configuration;
using ServiceControl.Hosting.Commands;
using ServiceControl.Infrastructure;

ILogger logger = null;

try
{
    ExeConfiguration.PopulateAppSettings(Assembly.GetExecutingAssembly());

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

    var arguments = new HostArguments(args);

    if (arguments.Help)
    {
        arguments.PrintUsage();
        return 0;
    }

    var settings = new Settings(loggingSettings: loggingSettings);

    await new CommandRunner(arguments.Command).Execute(arguments, settings);

    return Environment.ExitCode;
}
catch (Exception ex)
{
    if (logger != null)
    {
        logger.LogCritical(ex, "Unrecoverable error");
    }
    else
    {
        LoggingConfigurator.ConfigureNLog("bootstrap.txt", "./", NLog.LogLevel.Fatal);
        NLog.LogManager.GetCurrentClassLogger().Fatal(ex, "Unrecoverable error");
    }

    // The message goes to the console on its own, because the log above already holds the whole exception.
    // Rethrowing instead would print the stack trace a second time and fire the unhandled-exception handler
    // for a third, which buries a configuration mistake in what reads like a crash.
    await Console.Error.WriteLineAsync(ex.Message);

    return 1;
}
finally
{
    // The following log statement is meant to leave a trail in the logs to determine if the process was killed
    logger?.LogInformation("Shutdown complete");
    LoggerUtil.DisposeLoggerFactories();
}