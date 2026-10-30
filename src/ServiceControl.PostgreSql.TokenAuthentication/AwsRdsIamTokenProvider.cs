#nullable enable
namespace ServiceControl.PostgreSql.TokenAuthentication;

using System;
using System.Threading;
using System.Threading.Tasks;
using Amazon.RDS.Util;
using Amazon.Runtime;
using Npgsql;

class AwsRdsIamTokenProvider : IPostgreSqlTokenProvider
{
    public async ValueTask<string> GetToken(NpgsqlConnectionStringBuilder settings, CancellationToken cancellationToken = default)
    {
        var region = FallbackRegionFactory.GetRegionEndpoint()
            ?? throw new InvalidOperationException($"{PostgreSqlTokenAuthentication.AuthenticationKeyword}={PostgreSqlTokenAuthentication.AwsRdsIam} could not find the AWS region of the database server. Set the AWS_REGION environment variable, or a region in the AWS profile of the account ServiceControl runs as.");

        return await RDSAuthTokenGenerator.GenerateAuthTokenAsync(region, settings.Host, settings.Port, settings.Username);
    }
}
