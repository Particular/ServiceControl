namespace ServiceControl.UnitTests;

using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Raven.Client.Documents.Session;
using ServiceControl.Audit.Infrastructure;
using ServiceControl.Audit.Persistence.RavenDB;

class QueryTimeoutTests
{
    [Test]
    public void Should_cancel_query_that_exceeds_the_allowed_query_time()
    {
        var dataStore = new RavenAuditDataStore(new NeverCompletingSessionProvider(), BuildConfiguration(queryTimeout: TimeSpan.FromMilliseconds(50)));

        var exception = Assert.ThrowsAsync<TimeoutException>(() => dataStore.QueryMessages("search", new PagingInfo(), new SortInfo("time_sent", "desc"), new DateTimeRange((DateTime?)null, null)));

        Assert.That(exception.InnerException, Is.InstanceOf<OperationCanceledException>());
    }

    [Test]
    public void Should_propagate_caller_cancellation_instead_of_timing_out()
    {
        var dataStore = new RavenAuditDataStore(new NeverCompletingSessionProvider(), BuildConfiguration(queryTimeout: TimeSpan.FromSeconds(30)));

        using var callerTokenSource = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        var exception = Assert.CatchAsync(() => dataStore.QueryMessages("search", new PagingInfo(), new SortInfo("time_sent", "desc"), new DateTimeRange((DateTime?)null, null), callerTokenSource.Token));

        Assert.That(exception, Is.InstanceOf<OperationCanceledException>());
    }

    static DatabaseConfiguration BuildConfiguration(TimeSpan queryTimeout) =>
        new("audit", 60, true, TimeSpan.FromMinutes(5), 120000, 5, 5, new ServerConfiguration("http://localhost:33334"), TimeSpan.FromSeconds(60), queryTimeout);

    class NeverCompletingSessionProvider : IRavenSessionProvider
    {
        public async ValueTask<IAsyncDocumentSession> OpenSession(SessionOptions options = default, CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return null;
        }
    }
}
