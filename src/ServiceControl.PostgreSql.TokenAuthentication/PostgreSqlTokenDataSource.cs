#nullable enable
namespace ServiceControl.PostgreSql.TokenAuthentication;

using Npgsql;

static class PostgreSqlTokenDataSource
{
    // Asks for a token on every physical open: an AWS token stops working when the credentials that signed it expire, which can be minutes after signing.
    public static NpgsqlDataSource Build(string connectionString, IPostgreSqlTokenProvider tokenProvider) =>
        new NpgsqlDataSourceBuilder(connectionString)
            .UsePasswordProvider(settings => tokenProvider.GetToken(settings).AsTask().GetAwaiter().GetResult(), tokenProvider.GetToken)
            .Build();
}
