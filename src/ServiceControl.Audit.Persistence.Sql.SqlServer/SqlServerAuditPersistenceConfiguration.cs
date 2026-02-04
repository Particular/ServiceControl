namespace ServiceControl.Audit.Persistence.Sql.SqlServer;

public class SqlServerAuditPersistenceConfiguration : IPersistenceConfiguration
{
    const string DatabaseConnectionStringKey = "Database/ConnectionString";
    const string CommandTimeoutKey = "Database/CommandTimeout";
    const string MessageBodyStoragePathKey = "MessageBody/StoragePath";
    const string MinBodySizeForCompressionKey = "MessageBody/MinCompressionSize";
    const string StoreMessageBodiesOnDiskKey = "MessageBody/StoreOnDisk";

    public string Name => "SqlServer";

    public IEnumerable<string> ConfigurationKeys =>
    [
        DatabaseConnectionStringKey,
        CommandTimeoutKey,
        MessageBodyStoragePathKey,
        MinBodySizeForCompressionKey,
        StoreMessageBodiesOnDiskKey
    ];

    public IPersistence Create(PersistenceSettings settings)
    {
        var connectionString = GetRequiredSetting(settings, DatabaseConnectionStringKey);

        // Initialize message body storage path
        var messageBodyStoragePath = GetSetting(
            settings, MessageBodyStoragePathKey, Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "Particular", "ServiceControl.Audit", "bodies"));

        var specificSettings = new SqlServerAuditPersisterSettings(
            settings.AuditRetentionPeriod,
            settings.EnableFullTextSearchOnBodies,
            settings.MaxBodySizeToStore)
        {
            ConnectionString = connectionString,
            CommandTimeout = GetSetting(settings, CommandTimeoutKey, 30),
            MessageBodyStoragePath = messageBodyStoragePath,
            MinBodySizeForCompression = GetSetting(settings, MinBodySizeForCompressionKey, 4096),
            StoreMessageBodiesOnDisk = GetSetting(settings, StoreMessageBodiesOnDiskKey, true)
        };

        return new SqlServerAuditPersistence(specificSettings);
    }

    static string GetRequiredSetting(PersistenceSettings settings, string key)
    {
        if (settings.PersisterSpecificSettings.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        throw new Exception($"Setting {key} is required for SQL Server persistence. " +
            $"Set environment variable: SERVICECONTROL_AUDIT_DATABASE_CONNECTIONSTRING");
    }

    static string GetSetting(PersistenceSettings settings, string key, string defaultValue)
    {
        if (settings.PersisterSpecificSettings.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
        {
            return value;
        }
        return defaultValue;
    }

    static int GetSetting(PersistenceSettings settings, string key, int defaultValue)
    {
        if (settings.PersisterSpecificSettings.TryGetValue(key, out var value) && int.TryParse(value, out var intValue))
        {
            return intValue;
        }
        return defaultValue;
    }

    static bool GetSetting(PersistenceSettings settings, string key, bool defaultValue)
    {
        if (settings.PersisterSpecificSettings.TryGetValue(key, out var value) && bool.TryParse(value, out var boolValue))
        {
            return boolValue;
        }
        return defaultValue;
    }
}
