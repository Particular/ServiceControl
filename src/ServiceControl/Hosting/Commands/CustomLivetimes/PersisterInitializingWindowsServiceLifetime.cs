using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ServiceControl.Persistence;

sealed class PersisterInitializingWindowsServiceLifetime : WindowsServiceLifetime, IHostLifetime
{
    readonly IPersistenceLifecycle persistenceLifecycle;

    public PersisterInitializingWindowsServiceLifetime(
        IPersistenceLifecycle persistenceLifecycle,
        // base constructor
        IHostEnvironment environment, IHostApplicationLifetime applicationLifetime, ILoggerFactory loggerFactory, IOptions<HostOptions> optionsAccessor, IOptions<WindowsServiceLifetimeOptions> windowsServiceOptionsAccessor) : base(environment, applicationLifetime, loggerFactory, optionsAccessor, windowsServiceOptionsAccessor)
    {
        this.persistenceLifecycle = persistenceLifecycle;
    }

    public new async Task WaitForStartAsync(CancellationToken cancellationToken)
    {
        await base.WaitForStartAsync(cancellationToken);
        await persistenceLifecycle.Initialize(cancellationToken);
    }
}