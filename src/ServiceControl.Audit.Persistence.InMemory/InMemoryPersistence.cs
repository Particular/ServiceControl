namespace ServiceControl.Audit.Persistence.InMemory
{
    using System.Threading;
    using System.Threading.Tasks;
    using Auditing.BodyStorage;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using UnitOfWork;

    public class InMemoryPersistence(PersistenceSettings persistenceSettings) : IPersistence
    {
        public void Configure(IServiceCollection services)
        {
            services.AddSingleton(persistenceSettings);
            services.AddSingleton<InMemoryAuditDataStore>();
            services.AddSingleton<IAuditDataStore>(sp => sp.GetRequiredService<InMemoryAuditDataStore>());
            services.AddSingleton<IBodyStorage, InMemoryAttachmentsBodyStorage>();
            services.AddSingleton<IFailedAuditStorage, InMemoryFailedAuditStorage>();
            services.AddSingleton<IAuditIngestionUnitOfWorkFactory, InMemoryAuditIngestionUnitOfWorkFactory>();
            services.AddSingleton<IPersistenceLifecycle, InMemoryPersistenceLifecycle>();
        }

        public void ConfigureInstaller(IServiceCollection services)
        {
            services.AddHostedService<InMemoryPersistenceInstaller>();
        }
    }

    class InMemoryPersistenceInstaller(IHostApplicationLifetime applicationLifetime) : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken)
        {
            applicationLifetime.StopApplication();
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}