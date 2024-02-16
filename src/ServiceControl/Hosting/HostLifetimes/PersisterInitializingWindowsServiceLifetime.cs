#pragma warning disable CA1416

namespace ServiceControl.Hosting
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Hosting.WindowsServices;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using ServiceControl.Persistence;

    sealed class PersisterInitializingWindowsServiceLifetime(IHostEnvironment environment, IHostApplicationLifetime applicationLifetime, ILoggerFactory loggerFactory, IOptions<HostOptions> optionsAccessor, IOptions<WindowsServiceLifetimeOptions> windowsServiceOptionsAccessor, IPersistenceLifecycle persistenceLifecycle)
        : WindowsServiceLifetime(environment, applicationLifetime, loggerFactory, optionsAccessor, windowsServiceOptionsAccessor), IHostLifetime
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
#pragma warning restore CA1416