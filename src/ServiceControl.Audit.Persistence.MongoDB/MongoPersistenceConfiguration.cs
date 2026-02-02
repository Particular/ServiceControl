namespace ServiceControl.Audit.Persistence.MongoDB
{
    using System;
    using System.Collections.Generic;

    public class MongoPersistenceConfiguration : IPersistenceConfiguration
    {
        public const string ConnectionStringKey = "Database/ConnectionString";

        public IEnumerable<string> ConfigurationKeys =>
        [
            ConnectionStringKey
        ];

        public string Name => "MongoDB";

        public IPersistence Create(PersistenceSettings settings)
        {
            var mongoSettings = GetMongoSettings(settings);
            return new MongoPersistence(mongoSettings);
        }

        internal static MongoSettings GetMongoSettings(PersistenceSettings settings)
        {
            if (!settings.PersisterSpecificSettings.TryGetValue(ConnectionStringKey, out var connectionString))
            {
                throw new InvalidOperationException($"{ConnectionStringKey} must be specified.");
            }

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException($"{ConnectionStringKey} cannot be empty.");
            }

            // Extract database name from connection string, default to "audit" if not specified
            var mongoUrl = global::MongoDB.Driver.MongoUrl.Create(connectionString);
            var databaseName = string.IsNullOrWhiteSpace(mongoUrl.DatabaseName) ? "audit" : mongoUrl.DatabaseName;

            return new MongoSettings(
                connectionString,
                databaseName,
                settings.AuditRetentionPeriod,
                settings.EnableFullTextSearchOnBodies,
                settings.MaxBodySizeToStore);
        }
    }
}
