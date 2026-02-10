namespace ServiceControl.Persistence.Sql.SqlServer;

using System;
using System.IO;
using Configuration;
using ServiceControl.Persistence;

public class SqlServerPersistenceConfiguration : IPersistenceConfiguration
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
            throw new Exception($"Setting {DatabaseConnectionStringKey} is required for SQL Server persistence. " +
                $"Set environment variable: SERVICECONTROL_DATABASE_CONNECTIONSTRING");
        }

        // Initialize message body storage path
        var messageBodyStoragePath = SettingsReader.Read(
            settingsRootNamespace, MessageBodyStoragePathKey, Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "Particular", "ServiceControl", "bodies"));

        var settings = new SqlServerPersisterSettings
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
        var specificSettings = (SqlServerPersisterSettings)settings;
        return new SqlServerPersistence(specificSettings);
    }
}
