#nullable enable
namespace ServiceControl.PostgreSql.TokenAuthentication;

using System;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Logging;
using Npgsql;

class AzureEntraIdTokenProvider(TokenCredential credential, string scope, TimeProvider timeProvider, ILogger logger) : IPostgreSqlTokenProvider
{
    public AzureEntraIdTokenProvider(AzureEntraIdTokenAuthentication authentication, ILogger logger)
        : this(CreateCredential(authentication.AuthorityHost), authentication.Scope, TimeProvider.System, logger)
    {
    }

    public async ValueTask<string> GetToken(NpgsqlConnectionStringBuilder settings, CancellationToken cancellationToken = default)
    {
        var token = cached;

        if (token is not null && timeProvider.GetUtcNow() < token.RefreshAt)
        {
            return token.Value;
        }

        await refresh.WaitAsync(cancellationToken);

        try
        {
            var now = timeProvider.GetUtcNow();
            token = cached;

            if (token is not null && (now < token.RefreshAt || (now < nextAttempt && now < token.ExpiresOn)))
            {
                return token.Value;
            }

            nextAttempt = now + RetryInterval;

            try
            {
                var accessToken = await credential.GetTokenAsync(tokenRequest, cancellationToken);
                cached = new CachedToken(accessToken.Token, RefreshAt(accessToken), accessToken.ExpiresOn);

                return accessToken.Token;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception e) when (token is not null && now < token.ExpiresOn)
            {
                logger.LogWarning(e, "Could not get a new database access token. New connections keep using the current token, which expires at {ExpiresOn}. The next attempt is in {RetryInterval}", token.ExpiresOn, RetryInterval);

                return token.Value;
            }
        }
        finally
        {
            refresh.Release();
        }
    }

    static DateTimeOffset RefreshAt(AccessToken accessToken)
    {
        var beforeExpiry = accessToken.ExpiresOn - RefreshMargin;

        return accessToken.RefreshOn is { } refreshOn && refreshOn < beforeExpiry ? refreshOn : beforeExpiry;
    }

    static DefaultAzureCredential CreateCredential(Uri? authorityHost)
    {
        var options = new DefaultAzureCredentialOptions();

        if (authorityHost is not null)
        {
            options.AuthorityHost = authorityHost;
        }

        return new DefaultAzureCredential(options);
    }

    readonly TokenRequestContext tokenRequest = new([scope]);
    readonly SemaphoreSlim refresh = new(1, 1);
    CachedToken? cached;
    DateTimeOffset nextAttempt;

    // Under the 5 minutes before expiry in which MSAL stops handing out its cached token, so a refresh always gets a new one.
    static readonly TimeSpan RefreshMargin = TimeSpan.FromMinutes(2);
    static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(10);

    sealed record CachedToken(string Value, DateTimeOffset RefreshAt, DateTimeOffset ExpiresOn);
}
