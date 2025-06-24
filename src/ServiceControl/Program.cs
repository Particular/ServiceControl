using System;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Particular.ServiceControl.Hosting;
using ServiceBus.Management.Infrastructure.Settings;
using ServiceControl.Configuration;
using ServiceControl.Hosting.Commands;
using ServiceControl.Infrastructure;

var loggingSettings = new LoggingSettings(Settings.SettingsRootNamespace);
LoggingConfigurator.ConfigureLogging(loggingSettings);
var logger = LoggerUtil.CreateStaticLogger(typeof(Program));

try
{
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
    logger.LogCritical(ex, "Unrecoverable error");
    throw;
}
finally
{
    // The following log statement is meant to leave a trail in the logs to determine if the process was killed
    logger.LogInformation("Shutdown complete");
    NLog.LogManager.Shutdown();
}