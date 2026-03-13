namespace ServiceControl.Audit.Persistence.MongoDB
{
    using System;
    using System.Collections.Generic;

    public class MongoPersistenceConfiguration : IPersistenceConfiguration
    {
        public const string ConnectionStringKey = "Database/ConnectionString";
        public const string BodyStorageTypeKey = "Database/BodyStorageType";
        public const string BodyWriterBatchSizeKey = "Database/BodyWriterBatchSize";
        public const string BodyWriterParallelWritersKey = "Database/BodyWriterParallelWriters";
        public const string BodyWriterBatchTimeoutKey = "Database/BodyWriterBatchTimeout";
        public const string BlobConnectionStringKey = "Database/BlobConnectionString";
        public const string BlobContainerNameKey = "Database/BlobContainerName";

        public IEnumerable<string> ConfigurationKeys =>
        [
            ConnectionStringKey,
            BodyStorageTypeKey,
            BodyWriterBatchSizeKey,
            BodyWriterParallelWritersKey,
            BodyWriterBatchTimeoutKey,
            BlobConnectionStringKey,
            BlobContainerNameKey
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

            // Body storage type - defaults to Database. Accept "BLOB" as alias for Blob.
            var bodyStorageType = BodyStorageType.Database;
            if (settings.PersisterSpecificSettings.TryGetValue(BodyStorageTypeKey, out var bodyStorageTypeValue))
            {
                if (string.Equals(bodyStorageTypeValue?.Trim(), "BLOB", StringComparison.OrdinalIgnoreCase))
                {
                    bodyStorageType = BodyStorageType.Blob;
                }
                else if (Enum.TryParse<BodyStorageType>(bodyStorageTypeValue, ignoreCase: true, out var parsed))
                {
                    bodyStorageType = parsed;
                }
            }

            // Blob storage settings
            _ = settings.PersisterSpecificSettings.TryGetValue(BlobConnectionStringKey, out var blobConnectionString);
            _ = settings.PersisterSpecificSettings.TryGetValue(BlobContainerNameKey, out var blobContainerName);

            if (bodyStorageType == BodyStorageType.Blob && string.IsNullOrWhiteSpace(blobConnectionString))
            {
                throw new InvalidOperationException($"{BlobConnectionStringKey} must be specified when BodyStorageType is Blob.");
            }

            // Full text search requires Database body storage - bodies must be in MongoDB to be indexed
            var enableFullTextSearch = settings.EnableFullTextSearchOnBodies;
            if (bodyStorageType != BodyStorageType.Database && enableFullTextSearch)
            {
                enableFullTextSearch = false;
            }

            // Body writer settings - auto-calculate from TargetMessageIngestionRate if set, otherwise use defaults
            var hasExplicitBatchSize = settings.PersisterSpecificSettings.TryGetValue(BodyWriterBatchSizeKey, out var batchSizeValue);
            var hasExplicitWriters = settings.PersisterSpecificSettings.TryGetValue(BodyWriterParallelWritersKey, out var writersValue);

            var bodyWriterBatchSize = 500;
            var bodyWriterParallelWriters = 4;

            if (settings.TargetMessageIngestionRate is { } rate)
            {
                bodyWriterBatchSize = rate > 2000 ? 500 : 200;
                bodyWriterParallelWriters = rate <= 500 ? 2 : 4;
            }

            if (hasExplicitBatchSize && int.TryParse(batchSizeValue, out var parsedBatchSize))
            {
                bodyWriterBatchSize = parsedBatchSize;
            }

            if (hasExplicitWriters && int.TryParse(writersValue, out var parsedWriters))
            {
                bodyWriterParallelWriters = parsedWriters;
            }

            TimeSpan? bodyWriterBatchTimeout = null;
            if (settings.PersisterSpecificSettings.TryGetValue(BodyWriterBatchTimeoutKey, out var timeoutValue)
                && TimeSpan.TryParse(timeoutValue, out var parsedTimeout))
            {
                bodyWriterBatchTimeout = parsedTimeout;
            }

            return new MongoSettings(
                connectionString,
                databaseName,
                settings.AuditRetentionPeriod,
                enableFullTextSearch,
                settings.MaxBodySizeToStore,
                bodyStorageType,
                blobConnectionString,
                string.IsNullOrWhiteSpace(blobContainerName) ? "message-bodies" : blobContainerName,
                bodyWriterBatchSize,
                bodyWriterParallelWriters,
                bodyWriterBatchTimeout);
        }
    }
}
