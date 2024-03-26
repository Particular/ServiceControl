namespace ServiceControl.AcceptanceTests.TestSupport
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Hosting;
    using Persistence;

    class TestServerHostLifetime(IPersistenceLifecycle persistenceLifecycle) : IHostLifetime
    {
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task WaitForStartAsync(CancellationToken cancellationToken) => persistenceLifecycle.Initialize(cancellationToken);
    }
}