namespace ServiceControl.Audit.Persistence.Tests
{
    using System.Collections.Generic;
    using System.Configuration;
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

        public Task Configure()
        {
            var config = new InMemoryPersistenceConfiguration();
            var serviceCollection = new ServiceCollection();

            config.ConfigureServices(serviceCollection, new PersistenceSettings(new Dictionary<string, string>()));

            var serviceProvider = serviceCollection.BuildServiceProvider();
            AuditDataStore = serviceProvider.GetRequiredService<IAuditDataStore>();
            FailedAuditStorage = serviceProvider.GetRequiredService<IFailedAuditStorage>();
            BodyStorage = serviceProvider.GetService<IBodyStorage>();
            AuditIngestionUnitOfWorkFactory = serviceProvider.GetRequiredService<IAuditIngestionUnitOfWorkFactory>();
            return Task.CompletedTask;
        }

        public Task CompleteDBOperation() => Task.CompletedTask;

        public Task Cleanup() => Task.CompletedTask;

        public string ZipName => "InMemory";
    }
}