namespace ServiceControl.Persistence.SqlServer
{
    using System.Collections.Generic;
    using Microsoft.Extensions.DependencyInjection;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Persistence.UnitOfWork;

    public class SqlDbPersistenceConfiguration : IPersistenceConfiguration
    {
        public void ConfigureServices(IServiceCollection serviceCollection)
        {
            serviceCollection.AddSingleton(sp =>
                new SqlDbConnectionManager(SettingsReader<string>.Read("SqlStorageConnectionString")));
            serviceCollection.AddSingleton<IMonitoringDataStore, SqlDbMonitoringDataStore>();
            serviceCollection.AddSingleton<ICustomChecksDataStore, SqlDbCustomCheckDataStore>();
            serviceCollection.AddPartialUnitOfWorkFactory<SqlIngestionUnitOfWorkFactory>();
        }

        public string Name { get; }
        public IEnumerable<string> ConfigurationKeys { get; }
        public IPersistence Create(PersistenceSettings settings) => throw new System.NotImplementedException();
    }
}
