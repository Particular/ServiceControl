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

    // TODO: This constructor should not be needed and exist for backwards compability in the runtime
    // public WindowsServiceWithRequestTimeout(IHostEnvironment environment, IHostApplicationLifetime applicationLifetime, ILoggerFactory loggerFactory, IOptions<HostOptions> optionsAccessor)
    //     : this(environment, applicationLifetime, loggerFactory, optionsAccessor, Options.Create(new WindowsServiceLifetimeOptions()))
    // {
    // }

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
}