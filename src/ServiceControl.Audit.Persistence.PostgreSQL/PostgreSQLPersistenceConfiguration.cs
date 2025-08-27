namespace ServiceControl.Audit.Persistence.PostgreSQL;

using System;
using System.Collections.Generic;
using Npgsql;
using ServiceControl.Audit.Persistence;

public class PostgreSQLPersistenceConfiguration : IPersistenceConfiguration
{
    public string Name => "PostgreSQL";

    public IEnumerable<string> ConfigurationKeys => ["PostgreSql/ConnectionString", "PostgreSql/DatabaseName"];

    const int ExpirationProcessTimerInSecondsDefault = 600;

    public IPersistence Create(PersistenceSettings settings)
    {
        if (!settings.PersisterSpecificSettings.TryGetValue("PostgreSql/ConnectionString", out var connectionString))
        {
            throw new Exception("PostgreSql/ConnectionString is not configured.");
        }

        var builder = new NpgsqlConnectionStringBuilder(connectionString);

        if (settings.PersisterSpecificSettings.TryGetValue("PostgreSql/DatabaseName", out var databaseName))
        {
            builder.Database = databaseName;
        }

        settings.PersisterSpecificSettings.TryGetValue("PostgreSql/AdminDatabaseName", out var adminDatabaseName);

        builder.Database ??= "servicecontrol-audit";

        return new PostgreSQLPersistence(new DatabaseConfiguration(
            builder.Database,
            adminDatabaseName ?? "postgres",
            ExpirationProcessTimerInSecondsDefault,
            settings.AuditRetentionPeriod,
            settings.MaxBodySizeToStore,
            connectionString));
    }
}
