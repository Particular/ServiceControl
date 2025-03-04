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

    // TODO: I don't think this constructor is needed and exist for backwards compability in the runtime

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
        RequestAdditionalTime(hostOptions.ShutdownTimeout + CancellationDuration);
        base.OnStop();
    }

    protected override void OnShutdown()
    {
        RequestAdditionalTime(hostOptions.ShutdownTimeout + CancellationDuration);
        base.OnShutdown();
    }
}