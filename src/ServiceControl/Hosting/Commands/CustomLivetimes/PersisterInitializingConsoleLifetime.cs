using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ServiceControl.Persistence;

sealed class PersisterInitializingConsoleLifetime : ConsoleLifetime, IHostLifetime
{
    readonly IPersistenceLifecycle persistenceLifecycle;

    public PersisterInitializingConsoleLifetime(
        IPersistenceLifecycle persistenceLifecycle,
        // base constructor
        IOptions<ConsoleLifetimeOptions> options, IHostEnvironment environment, IHostApplicationLifetime applicationLifetime, IOptions<HostOptions> hostOptions, ILoggerFactory loggerFactory) : base(options, environment, applicationLifetime, hostOptions, loggerFactory
            )
    {
        this.persistenceLifecycle = persistenceLifecycle;
    }

    public new async Task WaitForStartAsync(CancellationToken cancellationToken)
    {
        await base.WaitForStartAsync(cancellationToken);
        await persistenceLifecycle.Initialize(cancellationToken);
    }
}