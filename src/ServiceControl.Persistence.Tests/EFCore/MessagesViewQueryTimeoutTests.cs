namespace ServiceControl.Persistence.Tests;

using System;
using System.Data.Common;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using ServiceControl.CompositeViews.Messages;
using ServiceControl.Persistence.EFCore.Abstractions;
using ServiceControl.Persistence.Infrastructure;

// Runs against the real provider: the way a cancelled command surfaces differs per ADO.NET provider
// (Microsoft.Data.SqlClient raises SqlException, Npgsql raises OperationCanceledException), which a stubbed
// session cannot show.
[TestFixture]
class MessagesViewQueryTimeoutTests : PersistenceTestBase
{
    readonly SlowCommands slowCommands = new();

    public MessagesViewQueryTimeoutTests() =>
        RegisterServices = services => PersistenceTestsContext.InterceptDatabaseCommands(services, slowCommands);

    [Test]
    public void A_search_over_the_query_time_limit_is_cancelled_and_reported_as_a_timeout_naming_the_setting()
    {
        Settings.QueryTimeout = TimeSpan.FromSeconds(1);
        slowCommands.DelaySql = PersistenceTestsContext.SqlToDelayFor(TimeSpan.FromSeconds(20));

        var stopwatch = Stopwatch.StartNew();
        var exception = Assert.ThrowsAsync<TimeoutException>(() => Search());
        stopwatch.Stop();

        Assert.That(stopwatch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(10)), "the query must be cancelled, not waited out");
        Assert.That(exception.Message, Does.Contain(PersistenceSettings.QueryTimeoutSettingName));
        Assert.That(slowCommands.CommandsSeen, Is.GreaterThan(0), "the interceptor did not see the command, so the query was not slowed down");
    }

    [Test]
    public async Task A_search_within_the_query_time_limit_returns_its_result()
    {
        Settings.QueryTimeout = TimeSpan.FromSeconds(30);

        var result = await Search();

        Assert.That(result.Results, Is.Empty);
    }

    [Test]
    public async Task The_per_command_timeout_of_a_search_is_the_query_time_limit()
    {
        // The provider's own command timeout (Database/CommandTimeout, default 30s) must not undercut the query
        // time limit, or raising QueryTimeoutInSeconds would have no effect and the error would not name it.
        Settings.QueryTimeout = TimeSpan.FromSeconds(123);

        await Search();

        Assert.That(slowCommands.CommandTimeoutSeen, Is.EqualTo(123));
    }

    EFPersisterSettings Settings => (EFPersisterSettings)PersistenceSettings;

    Task<QueryResult<System.Collections.Generic.IList<MessagesView>>> Search() =>
        ServiceProvider.GetRequiredService<IMessagesViewDataStore>()
            .GetAllMessagesForSearch("anything", new PagingInfo(), new SortInfo("time_sent", "desc"));

    // Prepends a server-side sleep to every query so the command is still running when the deadline fires.
    class SlowCommands : DbCommandInterceptor
    {
        public string DelaySql { get; set; }
        public int CommandsSeen { get; private set; }
        public int? CommandTimeoutSeen { get; private set; }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            CommandsSeen++;
            CommandTimeoutSeen = command.CommandTimeout;

            if (DelaySql != null)
            {
                command.CommandText = DelaySql + Environment.NewLine + command.CommandText;
            }

            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
