namespace ServiceControl.Persistence.Tests;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using Raven.Client.Documents.Session;
using ServiceControl.Persistence.Infrastructure;
using ServiceControl.Persistence.RavenDB;

class QueryTimeoutTests
{
    [Test]
    public void Should_cancel_query_that_exceeds_the_allowed_query_time()
    {
        var dataStore = BuildDataStore(queryTimeout: TimeSpan.FromMilliseconds(50));

        var exception = Assert.ThrowsAsync<TimeoutException>(() => dataStore.GetAllMessagesForSearch("search", new PagingInfo(), new SortInfo("time_sent", "desc"), timeSentRange: null));

        Assert.That(exception.InnerException, Is.InstanceOf<OperationCanceledException>());
    }

    [Test]
    public void Should_propagate_caller_cancellation_instead_of_timing_out()
    {
        var dataStore = BuildDataStore(queryTimeout: TimeSpan.FromSeconds(30));

        using var callerTokenSource = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        var exception = Assert.CatchAsync(() => dataStore.GetAllMessagesForSearch("search", new PagingInfo(), new SortInfo("time_sent", "desc"), timeSentRange: null, cancellationToken: callerTokenSource.Token));

        Assert.That(exception, Is.InstanceOf<OperationCanceledException>());
    }

    static ErrorMessagesDataStore BuildDataStore(TimeSpan queryTimeout)
    {
        var settings = new RavenPersisterSettings
        {
            ErrorRetentionPeriod = TimeSpan.FromDays(10),
            QueryTimeout = queryTimeout
        };

        return new ErrorMessagesDataStore(new NeverCompletingSessionProvider(), null, new ExpirationManager(settings), settings, NullLogger<ErrorMessagesDataStore>.Instance);
    }

    class NeverCompletingSessionProvider : IRavenSessionProvider
    {
        public async ValueTask<IAsyncDocumentSession> OpenSession(SessionOptions options = default, CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return null;
        }
    }
}
