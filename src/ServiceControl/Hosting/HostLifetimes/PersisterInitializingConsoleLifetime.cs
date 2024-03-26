namespace ServiceControl.Hosting
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Hosting.Internal;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using Persistence;

    sealed class PersisterInitializingConsoleLifetime(IOptions<ConsoleLifetimeOptions> options, IHostEnvironment environment, IHostApplicationLifetime applicationLifetime, IOptions<HostOptions> hostOptions, ILoggerFactory loggerFactory, IPersistenceLifecycle persistenceLifecycle)
        : ConsoleLifetime(options, environment, applicationLifetime, hostOptions, loggerFactory), IHostLifetime
    {
        public new async Task WaitForStartAsync(CancellationToken cancellationToken)
        {
            await base.WaitForStartAsync(cancellationToken);
            // Initialize needs to happen after WaitForStartAsync to ensure the lifetime implementation is invoked and
            // for windows services that process signals back to the service control manager (scm) that it is started.
            await persistenceLifecycle.Initialize(cancellationToken);
        }
    }
}