namespace ServiceControl.Audit.Persistence.EFCore.PostgreSql;

using System.Diagnostics.CodeAnalysis;
using ServiceControl.Audit.Persistence.EFCore.Abstractions;
using ServiceControl.PostgreSql.TokenAuthentication;

class PostgreSqlPersisterSettings : EFPersisterSettings
{
    /// <summary>
    /// Parses and removes the token authentication keywords, so every reader gets a string Npgsql accepts.
    /// </summary>
    [SetsRequiredMembers]
    public PostgreSqlPersisterSettings(EFPersisterSettings settings)
    {
        TokenAuthentication = PostgreSqlTokenAuthentication.Parse(settings.ConnectionString, EFPersistenceConfigurationBase.ConnectionStringKey, out var npgsqlConnectionString);
        ConnectionString = npgsqlConnectionString;
        Schema = settings.Schema;
        CommandTimeout = settings.CommandTimeout;
        QueryTimeout = settings.QueryTimeout;
        AuditRetentionPeriod = settings.AuditRetentionPeriod;
        BodyStorage = settings.BodyStorage;
        MaxBodySizeToStore = settings.MaxBodySizeToStore;
        MaxRetryCount = settings.MaxRetryCount;
        MaxRetryDelayInSeconds = settings.MaxRetryDelayInSeconds;
        EnableRetryOnFailure = settings.EnableRetryOnFailure;
        EnableSensitiveDataLogging = settings.EnableSensitiveDataLogging;
    }

    public PostgreSqlTokenAuthentication? TokenAuthentication { get; }
}
