namespace ServiceControl.Persistence.Tests.PostgreSql;

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Npgsql;
using NUnit.Framework;
using ServiceControl.PostgreSql.TokenAuthentication;

[TestFixture]
class AzureEntraIdTokenProviderTests
{
    [SetUp]
    public void SetUp()
    {
        time = new FakeTimeProvider(new DateTimeOffset(2026, 11, 3, 0, 0, 0, TimeSpan.Zero));
        credential = new FakeCredential(time);
        logger = new RecordingLogger();
        provider = new AzureEntraIdTokenProvider(credential, Scope, time, logger);
    }

    [Test]
    public async Task Reuses_a_token_until_two_minutes_before_it_expires()
    {
        var first = await GetToken();
        time.Advance(TimeSpan.FromMinutes(58) - TimeSpan.FromTicks(1));
        var beforeRefresh = await GetToken();
        time.Advance(TimeSpan.FromTicks(1));
        var afterRefresh = await GetToken();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(new[] { first, beforeRefresh, afterRefresh }, Is.EqualTo(new[] { "token-1", "token-1", "token-2" }));
            Assert.That(credential.Scopes.Distinct(), Is.EqualTo(new[] { Scope }));
        }
    }

    [Test]
    public async Task Refreshes_at_the_refresh_time_the_token_carries()
    {
        credential.Lifetime = TimeSpan.FromHours(24);
        credential.RefreshAfter = TimeSpan.FromHours(12);

        var first = await GetToken();
        time.Advance(TimeSpan.FromHours(12) - TimeSpan.FromTicks(1));
        var beforeRefresh = await GetToken();
        time.Advance(TimeSpan.FromTicks(1));
        var afterRefresh = await GetToken();

        Assert.That(new[] { first, beforeRefresh, afterRefresh }, Is.EqualTo(new[] { "token-1", "token-1", "token-2" }));
    }

    [Test]
    public async Task Keeps_the_current_token_and_retries_every_10_seconds_while_a_refresh_fails()
    {
        await GetToken();
        var failure = new InvalidOperationException("Entra ID is unavailable");
        credential.Failure = failure;
        time.Advance(TimeSpan.FromMinutes(58));

        var duringFailure = await GetToken();
        time.Advance(TimeSpan.FromSeconds(10) - TimeSpan.FromTicks(1));
        var beforeRetry = await GetToken();
        var callsBeforeRetry = credential.Calls;
        credential.Failure = null;
        time.Advance(TimeSpan.FromTicks(1));
        var afterRetry = await GetToken();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(new[] { duringFailure, beforeRetry, afterRetry }, Is.EqualTo(new[] { "token-1", "token-1", "token-2" }));
            Assert.That(callsBeforeRetry, Is.EqualTo(2), "The credential is not asked again within 10 seconds of a failure.");
            Assert.That(logger.Warnings.Select(warning => warning.Exception), Is.EqualTo(new[] { failure }));
            Assert.That(logger.Warnings.Single().Message, Does.Contain("for user sc on host sc.postgres.database.azure.com, database servicecontrol.").And.Not.Contain("token-1"));
        }
    }

    [Test]
    public async Task Fails_when_a_refresh_fails_after_the_token_expired()
    {
        await GetToken();
        var failure = new InvalidOperationException("Entra ID is unavailable");
        credential.Failure = failure;
        time.Advance(TimeSpan.FromHours(1));

        var exception = Assert.CatchAsync(async () => await GetToken());

        Assert.That(exception, Is.SameAs(failure));
    }

    [Test]
    public async Task Asks_the_credential_once_for_concurrent_connections()
    {
        var release = new TaskCompletionSource();
        credential.Gate = release.Task;

        var tokens = Enumerable.Range(0, 3).Select(_ => GetToken().AsTask()).ToArray();
        release.SetResult();

        Assert.That(await Task.WhenAll(tokens), Is.All.EqualTo("token-1"));
        Assert.That(credential.Calls, Is.EqualTo(1));
    }

    ValueTask<string> GetToken() => provider.GetToken(new NpgsqlConnectionStringBuilder("Host=sc.postgres.database.azure.com;Username=sc;Database=servicecontrol"));

    const string Scope = "https://ossrdbms-aad.database.windows.net/.default";

    FakeTimeProvider time;
    FakeCredential credential;
    RecordingLogger logger;
    AzureEntraIdTokenProvider provider;

    sealed class FakeCredential(TimeProvider time) : TokenCredential
    {
        int calls;
        int issued;

        public int Calls => Volatile.Read(ref calls);

        public ConcurrentQueue<string> Scopes { get; } = new();

        public TimeSpan Lifetime { get; set; } = TimeSpan.FromHours(1);

        public TimeSpan? RefreshAfter { get; set; }

        public Exception Failure { get; set; }

        public Task Gate { get; set; } = Task.CompletedTask;

        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public override async ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref calls);
            Scopes.Enqueue(requestContext.Scopes.Single());

            await Gate;

            if (Failure is not null)
            {
                throw Failure;
            }

            var now = time.GetUtcNow();

            return new AccessToken($"token-{Interlocked.Increment(ref issued)}", now + Lifetime, now + RefreshAfter);
        }
    }

    sealed class RecordingLogger : ILogger
    {
        readonly ConcurrentQueue<(Exception Exception, string Message)> warnings = new();

        public (Exception Exception, string Message)[] Warnings => [.. warnings];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (logLevel == LogLevel.Warning)
            {
                warnings.Enqueue((exception, formatter(state, exception)));
            }
        }
    }
}
