namespace ServiceControl.Audit.Persistence.MongoDB
{
    using System;
    using System.Collections.Generic;

    public class MongoPersistenceConfiguration : IPersistenceConfiguration
    {
        public const string ConnectionStringKey = "Database/ConnectionString";
        public const string BodyStorageTypeKey = "Database/BodyStorageType";
        public const string BodyStoragePathKey = "Database/BodyStoragePath";

        public IEnumerable<string> ConfigurationKeys =>
        [
            ConnectionStringKey,
            BodyStorageTypeKey,
            BodyStoragePathKey
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

            // Body storage type - defaults to Database
            var bodyStorageType = BodyStorageType.Database;
            if (settings.PersisterSpecificSettings.TryGetValue(BodyStorageTypeKey, out var bodyStorageTypeValue))
            {
                if (Enum.TryParse<BodyStorageType>(bodyStorageTypeValue, ignoreCase: true, out var parsed))
                {
                    bodyStorageType = parsed;
                }
            }

            // Body storage path - required for FileSystem storage
            _ = settings.PersisterSpecificSettings.TryGetValue(BodyStoragePathKey, out var bodyStoragePath);

            if (bodyStorageType == BodyStorageType.FileSystem && string.IsNullOrWhiteSpace(bodyStoragePath))
            {
                throw new InvalidOperationException($"{BodyStoragePathKey} must be specified when BodyStorageType is FileSystem.");
            }

            return new MongoSettings(
                connectionString,
                databaseName,
                settings.AuditRetentionPeriod,
                settings.EnableFullTextSearchOnBodies,
                settings.MaxBodySizeToStore,
                bodyStorageType,
                bodyStoragePath);
        }
    }
}
