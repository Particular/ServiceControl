namespace ServiceControl.Persistence.Tests;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using Raven.Client.Exceptions.Database;
using ServiceControl.RavenDB;

[TestFixture]
public class DatabaseLoadRetryTests
{
    [Test]
    public async Task Should_retry_when_database_load_times_out()
    {
        var attempts = 0;

        await DatabaseLoadRetry.Execute(_ =>
        {
            attempts++;
            return attempts < 3 ? Task.FromException(new DatabaseLoadTimeoutException("Database is loading")) : Task.CompletedTask;
        }, NullLogger.Instance, TimeSpan.FromMilliseconds(1));

        Assert.That(attempts, Is.EqualTo(3));
    }

    [Test]
    public void Should_not_retry_other_exceptions()
    {
        var attempts = 0;

        Assert.ThrowsAsync<InvalidOperationException>(() => DatabaseLoadRetry.Execute(_ =>
        {
            attempts++;
            return Task.FromException(new InvalidOperationException());
        }, NullLogger.Instance, TimeSpan.FromMilliseconds(1)));

        Assert.That(attempts, Is.EqualTo(1));
    }

    [Test]
    public void Should_stop_retrying_when_cancelled()
    {
        using var tokenSource = new CancellationTokenSource();
        var attempts = 0;

        Assert.CatchAsync<OperationCanceledException>(() => DatabaseLoadRetry.Execute(_ =>
        {
            attempts++;
            tokenSource.Cancel();
            return Task.FromException(new DatabaseLoadTimeoutException("Database is loading"));
        }, NullLogger.Instance, TimeSpan.FromMinutes(1), tokenSource.Token));

        Assert.That(attempts, Is.EqualTo(1));
    }
}
