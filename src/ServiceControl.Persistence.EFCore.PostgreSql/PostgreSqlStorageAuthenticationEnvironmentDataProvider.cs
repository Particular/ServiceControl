namespace ServiceControl.Persistence.EFCore.PostgreSql;

using Npgsql;
using Particular.LicensingComponent.Contracts;
using static Particular.LicensingComponent.Contracts.EnvironmentDatum;

class PostgreSqlStorageAuthenticationEnvironmentDataProvider(PostgreSqlPersisterSettings settings) : IEnvironmentDataProvider
{
    public IEnumerable<EnvironmentDatum> GetData() =>
    [
        Value("Storage.Auth", Authentication)
    ];

    string Authentication()
    {
        var builder = new NpgsqlConnectionStringBuilder(settings.ConnectionString);

        if (!string.IsNullOrEmpty(builder.SslCertificate))
        {
            return "ClientCertificate";
        }

        return !string.IsNullOrEmpty(builder.Password) || !string.IsNullOrEmpty(builder.Passfile) ? "Password" : "None";
    }
}
