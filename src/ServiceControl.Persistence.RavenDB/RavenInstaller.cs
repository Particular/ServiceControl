namespace ServiceControl.Persistence.RavenDB
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using ServiceControl.Persistence;

    class RavenInstaller : IPersistenceInstaller
    {
        public RavenInstaller(ServiceCollection services)
        {
            this.services = services;
        }

        public async Task Install(CancellationToken cancellationToken)
        {
            using var serviceProvider = services.BuildServiceProvider();

            var lifecycle = serviceProvider.GetRequiredService<IPersistenceLifecycle>();
            await lifecycle.Initialize(cancellationToken);
        }

        readonly ServiceCollection services;
    }
}