namespace ServiceControl.Persistence.Sql.SqlServer;

using Configuration;
using ServiceControl.Persistence;

public class SqlServerPersistenceConfiguration : IPersistenceConfiguration
{
    const string DatabaseConnectionStringKey = "Database/ConnectionString";
    const string CommandTimeoutKey = "Database/CommandTimeout";

    public PersistenceSettings CreateSettings(SettingsRootNamespace settingsRootNamespace)
    {
        var connectionString = SettingsReader.Read<string>(settingsRootNamespace, DatabaseConnectionStringKey);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new Exception($"Setting {DatabaseConnectionStringKey} is required for SQL Server persistence. " +
                $"Set environment variable: SERVICECONTROL_DATABASE_CONNECTIONSTRING");
        }

        var settings = new SqlServerPersisterSettings
        {
            ConnectionString = connectionString,
            CommandTimeout = SettingsReader.Read(settingsRootNamespace, CommandTimeoutKey, 30),
        };

        return settings;
    }

    public IPersistence Create(PersistenceSettings settings)
    {
        var specificSettings = (SqlServerPersisterSettings)settings;
        return new SqlServerPersistence(specificSettings);
    }
}
