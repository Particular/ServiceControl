namespace ServiceControl.Persistence.RavenDb
{
    using System.Collections.Generic;

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

        public IPersistence Create(PersistenceSettings settings) => throw new System.NotImplementedException();
    }
}
