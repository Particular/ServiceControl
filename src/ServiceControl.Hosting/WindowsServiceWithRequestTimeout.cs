namespace ServiceControl.Hosting;

using System;
using System.Runtime.Versioning;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

[SupportedOSPlatform("windows")]
sealed class WindowsServiceWithRequestTimeout : WindowsServiceLifetime
{
    static readonly TimeSpan CancellationDuration = TimeSpan.FromSeconds(5);
    readonly HostOptions hostOptions;

    public WindowsServiceWithRequestTimeout(IHostEnvironment environment, IHostApplicationLifetime applicationLifetime, ILoggerFactory loggerFactory, IOptions<HostOptions> optionsAccessor, IOptions<WindowsServiceLifetimeOptions> windowsServiceOptionsAccessor)
        : base(environment, applicationLifetime, loggerFactory, optionsAccessor, windowsServiceOptionsAccessor)
    {
        hostOptions = optionsAccessor.Value;
    }

    protected override void OnStop()
    {
        var logger = NLog.LogManager.GetCurrentClassLogger();
        var additionalTime = hostOptions.ShutdownTimeout + CancellationDuration;

        logger.Info("OnStop invoked, going to ask for additional time: {additionalTime}", additionalTime);
        RequestAdditionalTime(additionalTime);
        logger.Info("Additional time requested");

        base.OnStop();
    }

    protected override void OnShutdown()
    {
        var logger = NLog.LogManager.GetCurrentClassLogger();
        logger.Info("OnShutdown invoked, process may exit ungracefully");
        base.OnShutdown();
    }
}