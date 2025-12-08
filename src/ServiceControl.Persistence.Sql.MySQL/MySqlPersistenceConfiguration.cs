namespace ServiceControl.Persistence.Sql.MySQL;

using Configuration;
using ServiceControl.Persistence;

public class MySqlPersistenceConfiguration : IPersistenceConfiguration
{
    const string DatabaseConnectionStringKey = "Database/ConnectionString";
    const string CommandTimeoutKey = "Database/CommandTimeout";

    public PersistenceSettings CreateSettings(SettingsRootNamespace settingsRootNamespace)
    {
        var connectionString = SettingsReader.Read<string>(settingsRootNamespace, DatabaseConnectionStringKey);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new Exception($"Setting {DatabaseConnectionStringKey} is required for MySQL persistence. " +
                $"Set environment variable: SERVICECONTROL_DATABASE_CONNECTIONSTRING");
        }

        var settings = new MySqlPersisterSettings
        {
            ConnectionString = connectionString,
            CommandTimeout = SettingsReader.Read(settingsRootNamespace, CommandTimeoutKey, 30),
        };

        return settings;
    }

    public IPersistence Create(PersistenceSettings settings)
    {
        var specificSettings = (MySqlPersisterSettings)settings;
        return new MySqlPersistence(specificSettings);
    }
}
