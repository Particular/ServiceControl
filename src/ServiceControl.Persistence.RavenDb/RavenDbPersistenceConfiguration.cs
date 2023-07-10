namespace ServiceControl.Persistence.RavenDb
{
    using System.Collections.Generic;
    using Microsoft.Extensions.DependencyInjection;
    using ServiceControl.Persistence.UnitOfWork;

    public class RavenDbPersistenceConfiguration : IPersistenceConfiguration
    {
        //TODO: figure out what can be strongly typed
        public const string LogPathKey = "LogPath";
        public const string DbPathKey = "DbPath";
        public const string DataSpaceRemainingThresholdKey = "DataSpaceRemainingThreshold";
        public const string MinimumStorageLeftRequiredForIngestionKey = "MinimumStorageLeftRequiredForIngestion";
        public const string AuditRetentionPeriodKey = "AuditRetentionPeriod";

        public string Name => throw new System.NotImplementedException();

        public IEnumerable<string> ConfigurationKeys => throw new System.NotImplementedException();

        public void ConfigureServices(IServiceCollection serviceCollection)
        {
            serviceCollection.AddSingleton<IMonitoringDataStore, RavenDbMonitoringDataStore>();
            serviceCollection.AddSingleton<ICustomChecksDataStore, RavenDbCustomCheckDataStore>();
            serviceCollection.AddUnitOfWorkFactory<RavenDbIngestionUnitOfWorkFactory>();
            serviceCollection.AddSingleton<MinimumRequiredStorageState>();
        }

        public IPersistence Create(PersistenceSettings settings) => throw new System.NotImplementedException();
    }
}
