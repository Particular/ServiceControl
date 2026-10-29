#nullable enable
namespace ServiceControl.PostgreSql.TokenAuthentication;

using System;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;
using Npgsql;

class AzureEntraIdTokenProvider(AzureEntraIdTokenAuthentication authentication) : IPostgreSqlTokenProvider
{
    readonly TokenCredential credential = CreateCredential(authentication.AuthorityHost);
    readonly TokenRequestContext tokenRequest = new([authentication.Scope]);

    public async ValueTask<string> GetToken(NpgsqlConnectionStringBuilder settings, CancellationToken cancellationToken = default) =>
        (await credential.GetTokenAsync(tokenRequest, cancellationToken)).Token;

    static DefaultAzureCredential CreateCredential(Uri? authorityHost)
    {
        var options = new DefaultAzureCredentialOptions();

        if (authorityHost is not null)
        {
            options.AuthorityHost = authorityHost;
        }

        return new DefaultAzureCredential(options);
    }
}
