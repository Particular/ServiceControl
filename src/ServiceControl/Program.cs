using System;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Particular.ServiceControl.Hosting;
using ServiceBus.Management.Infrastructure.Settings;
using ServiceControl.Configuration;
using ServiceControl.Hosting.Commands;
using ServiceControl.Infrastructure;

ILogger logger = null;

try
{
    var bootstrapConfig = new ConfigurationBuilder()
        .SetBasePath(AppContext.BaseDirectory)
        .AddLegacyAppSettings()
        .AddEnvironmentVariables()
        .Build();

    var section = bootstrapConfig.GetSection(SettingsConfiguration.SectionName);
    var bootstrapSettings = section.Get<Settings>();

    var loggingSettings = LoggingSettingsFactory.Create(section);
    bootstrapSettings.LoggingSettings = loggingSettings;    // TODO: Remove this when we have a better way to pass logging settings to the host

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

    var arguments = new HostArguments(args, bootstrapSettings);

    if (arguments.Help)
    {
        arguments.PrintUsage();
        return 0;
    }

    await new CommandRunner(arguments.Command).Execute(arguments, bootstrapSettings);

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
