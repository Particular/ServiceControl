namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using System.Threading.Tasks;
    using Auditing.BodyStorage;
    using Microsoft.Extensions.DependencyInjection;
    using ServiceControl.Audit.Persistence.InMemory;
    using UnitOfWork;

    partial class PersistenceTestsConfiguration
    {
        public IAuditDataStore AuditDataStore { get; protected set; }
        public IFailedAuditStorage FailedAuditStorage { get; protected set; }
        public IBodyStorage BodyStorage { get; protected set; }
        public IAuditIngestionUnitOfWorkFactory AuditIngestionUnitOfWorkFactory { get; protected set; }

        public Task Configure(Action<PersistenceSettings> setSettings)
        {
            var config = new InMemoryPersistenceConfiguration();
            var serviceCollection = new ServiceCollection();
            var settings = new PersistenceSettings(TimeSpan.FromHours(1), true, 100000);

            setSettings(settings);
            var persistence = config.Create(settings);
            persistence.Configure(serviceCollection);

            var serviceProvider = serviceCollection.BuildServiceProvider();
            AuditDataStore = serviceProvider.GetRequiredService<IAuditDataStore>();
            FailedAuditStorage = serviceProvider.GetRequiredService<IFailedAuditStorage>();
            BodyStorage = serviceProvider.GetService<IBodyStorage>();
            AuditIngestionUnitOfWorkFactory = serviceProvider.GetRequiredService<IAuditIngestionUnitOfWorkFactory>();
            return Task.CompletedTask;
        }

        public Task CompleteDBOperation()
        {
            return Task.CompletedTask;
        }

        public Task Cleanup()
        {
            return Task.CompletedTask;
        }

        public string Name => "InMemory";
    }
}