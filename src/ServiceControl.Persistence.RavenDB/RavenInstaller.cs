namespace ServiceControl.Persistence.RavenDB
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using ServiceControl.Persistence;

    class RavenInstaller(IServiceCollection services) : IPersistenceInstaller
    {
        public async Task Install(CancellationToken cancellationToken)
        {
            await using var serviceProvider = services.BuildServiceProvider();

            var lifecycle = serviceProvider.GetRequiredService<IPersistenceLifecycle>();
            await lifecycle.Initialize(cancellationToken);
        }
    }
}