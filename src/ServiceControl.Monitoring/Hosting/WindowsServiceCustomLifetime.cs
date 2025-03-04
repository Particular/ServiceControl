namespace ServiceControl.Monitoring.Hosting;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

#pragma warning disable CA1416
class WindowsServiceCustomLifetime : WindowsServiceLifetime
{
    public WindowsServiceCustomLifetime(IHostEnvironment environment, IHostApplicationLifetime applicationLifetime, ILoggerFactory loggerFactory, IOptions<HostOptions> optionsAccessor, Settings settings)
        : base(environment, applicationLifetime, loggerFactory, optionsAccessor)
    {
        this.settings = settings;
    }

    public WindowsServiceCustomLifetime(IHostEnvironment environment, IHostApplicationLifetime applicationLifetime, ILoggerFactory loggerFactory, IOptions<HostOptions> optionsAccessor, IOptions<WindowsServiceLifetimeOptions> windowsServiceOptionsAccessor, Settings settings)
        : base(environment, applicationLifetime, loggerFactory, optionsAccessor, windowsServiceOptionsAccessor)
    {
        this.settings = settings;
    }

    protected override void OnStop()
    {
        RequestAdditionalTime(settings.ShutdownTimeout);
        base.OnStop();
    }

    readonly Settings settings;
}
#pragma warning restore CA1416