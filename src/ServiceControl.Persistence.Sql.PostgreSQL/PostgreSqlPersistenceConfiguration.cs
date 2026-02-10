namespace ServiceControl.Persistence.Sql.PostgreSQL;

using Configuration;
using ServiceControl.Persistence;

public class PostgreSqlPersistenceConfiguration : IPersistenceConfiguration
{
    const string DatabaseConnectionStringKey = "Database/ConnectionString";
    const string CommandTimeoutKey = "Database/CommandTimeout";
    const string MessageBodyStoragePathKey = "MessageBody/StoragePath";
    const string MinBodySizeForCompressionKey = "MessageBody/MinCompressionSize";

    public PersistenceSettings CreateSettings(SettingsRootNamespace settingsRootNamespace)
    {
        var connectionString = SettingsReader.Read<string>(settingsRootNamespace, DatabaseConnectionStringKey);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new Exception($"Setting {DatabaseConnectionStringKey} is required for PostgreSQL persistence. " +
                $"Set environment variable: SERVICECONTROL_DATABASE_CONNECTIONSTRING");
        }

        // Initialize message body storage path
        var messageBodyStoragePath = SettingsReader.Read(
            settingsRootNamespace, MessageBodyStoragePathKey, Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "Particular", "ServiceControl", "bodies"));


        var settings = new PostgreSqlPersisterSettings
        {
            ConnectionString = connectionString,
            CommandTimeout = SettingsReader.Read(settingsRootNamespace, CommandTimeoutKey, 30),
            MessageBodyStoragePath = messageBodyStoragePath,
            MinBodySizeForCompression = SettingsReader.Read(settingsRootNamespace, MinBodySizeForCompressionKey, 4096)
        };

        return settings;
    }

    public IPersistence Create(PersistenceSettings settings)
    {
        var specificSettings = (PostgreSqlPersisterSettings)settings;
        return new PostgreSqlPersistence(specificSettings);
    }
}
