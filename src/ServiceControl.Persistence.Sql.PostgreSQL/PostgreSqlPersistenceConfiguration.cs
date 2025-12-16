namespace ServiceControl.Persistence.Sql.PostgreSQL;

using Configuration;
using ServiceControl.Persistence;

public class PostgreSqlPersistenceConfiguration : IPersistenceConfiguration
{
    const string DatabaseConnectionStringKey = "Database/ConnectionString";
    const string CommandTimeoutKey = "Database/CommandTimeout";

    public PersistenceSettings CreateSettings(SettingsRootNamespace settingsRootNamespace)
    {
        var connectionString = SettingsReader.Read<string>(settingsRootNamespace, DatabaseConnectionStringKey);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new Exception($"Setting {DatabaseConnectionStringKey} is required for PostgreSQL persistence. " +
                $"Set environment variable: SERVICECONTROL_DATABASE_CONNECTIONSTRING");
        }

        var settings = new PostgreSqlPersisterSettings
        {
            ConnectionString = connectionString,
            CommandTimeout = SettingsReader.Read(settingsRootNamespace, CommandTimeoutKey, 30),
        };

        return settings;
    }

    public IPersistence Create(PersistenceSettings settings)
    {
        var specificSettings = (PostgreSqlPersisterSettings)settings;
        return new PostgreSqlPersistence(specificSettings);
    }
}
