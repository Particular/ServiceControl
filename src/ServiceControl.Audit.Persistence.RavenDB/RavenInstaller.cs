namespace ServiceControl.Audit.Persistence.RavenDB
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;

    class RavenInstaller(IServiceCollection services) : IPersistenceInstaller
    {
        public async Task Install(CancellationToken cancellationToken)
        {
            await using var serviceProvider = services.BuildServiceProvider();

            var lifecycle = serviceProvider.GetRequiredService<IPersistenceLifecycle>();
            await lifecycle.Start(cancellationToken);
            await lifecycle.Stop(cancellationToken);
        }
    }
}