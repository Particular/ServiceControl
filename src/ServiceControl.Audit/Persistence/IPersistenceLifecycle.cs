namespace ServiceControl.Audit.Persistence
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;

    interface IPersistenceLifecycle
    {
        Task Initialize();
    }

    static class PersistenceLifecycleServiceCollectionExtensions
    {
        public static void AddPersistenceLifecycle<TLifecycle>(this IServiceCollection services)
            where TLifecycle : class, IPersistenceLifecycle
        {
            services.AddSingleton<TLifecycle>();
            services.AddSingleton<IPersistenceLifecycle>(sp => sp.GetRequiredService<TLifecycle>());
            services.AddHostedService<PersistenceLifecycleHostedService<TLifecycle>>();
        }
    }

    class PersistenceLifecycleHostedService<TLifecycle> : IHostedService
        where TLifecycle : class, IPersistenceLifecycle
    {
        TLifecycle lifecycle;

        public PersistenceLifecycleHostedService(TLifecycle lifecycle) => this.lifecycle = lifecycle;

        public Task StartAsync(CancellationToken cancellationToken) => lifecycle.Initialize();

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            if (lifecycle is IAsyncDisposable disposable)
            {
                await disposable.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}