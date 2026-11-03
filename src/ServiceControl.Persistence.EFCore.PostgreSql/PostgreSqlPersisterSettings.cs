namespace ServiceControl.Persistence.EFCore.PostgreSql;

using ServiceControl.Persistence.EFCore.Abstractions;
using ServiceControl.PostgreSql.TokenAuthentication;

public class PostgreSqlPersisterSettings : EFPersisterSettings
{
    /// <summary>
    /// Assigning parses and removes the token authentication keywords, so every reader gets a string Npgsql accepts.
    /// </summary>
    public override required string ConnectionString
    {
        get;
        set
        {
            TokenAuthentication = PostgreSqlTokenAuthentication.Parse(value, EFPersistenceConfigurationBase.ConnectionStringKey, out var npgsqlConnectionString);
            field = npgsqlConnectionString;
        }
    }

    internal PostgreSqlTokenAuthentication? TokenAuthentication { get; private set; }
}
