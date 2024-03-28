namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using System.Threading.Tasks;
    using Auditing.BodyStorage;
    using Microsoft.Extensions.DependencyInjection;
    using ServiceControl.Audit.Persistence.InMemory;
    using UnitOfWork;

    class PersistenceTestsConfiguration
    {
        public IAuditDataStore AuditDataStore { get; private set; }
        public IFailedAuditStorage FailedAuditStorage { get; private set; }
        public IBodyStorage BodyStorage { get; private set; }
        public IAuditIngestionUnitOfWorkFactory AuditIngestionUnitOfWorkFactory { get; private set; }
        public IServiceProvider ServiceProvider { get; private set; }
        public string Name => "InMemory";

        public Task Configure(Action<PersistenceSettings> setSettings)
        {
            var config = new InMemoryPersistenceConfiguration();
            var serviceCollection = new ServiceCollection();
            var settings = new PersistenceSettings(TimeSpan.FromHours(1), true, 100000);

            setSettings(settings);
            var persistence = config.Create(settings);
            persistence.AddPersistence(serviceCollection);

            ServiceProvider = serviceCollection.BuildServiceProvider();
            AuditDataStore = ServiceProvider.GetRequiredService<IAuditDataStore>();
            FailedAuditStorage = ServiceProvider.GetRequiredService<IFailedAuditStorage>();
            BodyStorage = ServiceProvider.GetService<IBodyStorage>();
            AuditIngestionUnitOfWorkFactory = ServiceProvider.GetRequiredService<IAuditIngestionUnitOfWorkFactory>();
            return Task.CompletedTask;
        }

        public Task CompleteDBOperation() => Task.CompletedTask;

        public Task Cleanup() => Task.CompletedTask;
    }
}