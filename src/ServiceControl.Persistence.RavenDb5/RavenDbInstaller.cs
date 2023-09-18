namespace ServiceControl.Persistence.RavenDb5
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using ServiceControl.Persistence;

    class RavenDbInstaller : IPersistenceInstaller
    {
        public RavenDbInstaller(ServiceCollection services)
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