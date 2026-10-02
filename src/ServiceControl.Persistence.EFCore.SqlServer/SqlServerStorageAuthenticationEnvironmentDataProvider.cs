namespace ServiceControl.Persistence.EFCore.SqlServer;

using Microsoft.Data.SqlClient;
using Particular.LicensingComponent.Contracts;
using static Particular.LicensingComponent.Contracts.EnvironmentDatum;

class SqlServerStorageAuthenticationEnvironmentDataProvider(SqlServerPersisterSettings settings) : IEnvironmentDataProvider
{
    public IEnumerable<EnvironmentDatum> GetData() =>
    [
        Value("Storage.Auth", Authentication)
    ];

    string Authentication()
    {
        var builder = new SqlConnectionStringBuilder(settings.ConnectionString);

        if (builder.Authentication is SqlAuthenticationMethod.NotSpecified)
        {
            return builder.IntegratedSecurity ? "Integrated" : "SqlPassword";
        }

        return builder.Authentication is SqlAuthenticationMethod.SqlPassword ? "SqlPassword" : "EntraId";
    }
}
