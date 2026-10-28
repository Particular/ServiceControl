// ReSharper disable once CheckNamespace
namespace ServiceControl.Persistence.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using ServiceControl.MessageFailures;
using ServiceControl.Persistence.DataMigration;
using ServiceControl.Persistence.EFCore.DbContexts;
using ServiceControl.Persistence.EFCore.Entities;
using ServiceControl.Persistence.EFCore.Infrastructure;

class MigrationPlanReuseTests : PersistenceTestBase
{
    static readonly MigrationCategory FailedMessagesCategory = MigrationCategoryRegistry.All.Single(category => category.Id == MigrationCategoryIds.UnresolvedAndRetryIssuedFailedMessages);

    IMigrationTarget Target => ServiceProvider.GetRequiredService<IMigrationTarget>();

    [SetUp]
    public Task OpenTarget() => Target.Open();

    [Test]
    public async Task Batches_whose_unbounded_text_sits_either_side_of_4000_characters_share_one_cached_plan()
    {
        var shortHeaders = new IngestedFailure();
        shortHeaders.Headers["Padding"] = new string('h', 3_000);
        var longHeaders = new IngestedFailure { Groups = [new() { Id = Guid.NewGuid().ToString(), Title = new string('t', 5_000), Type = "Message Type" }] };
        longHeaders.Headers["Padding"] = new string('h', 5_000);

        await Target.Write(FailedMessagesCategory, BatchOf(shortHeaders.ToFailedMessage()), Checkpoint());
        var saved = await ServiceProvider.GetRequiredService<IMigrationCheckpointStore>().Read(MigrationCategoryIds.UnresolvedAndRetryIssuedFailedMessages);
        var result = await Target.Write(FailedMessagesCategory, BatchOf(longHeaders.ToFailedMessage()), saved);

        using var scope = ServiceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();
        var headersJsonLengths = await db.FailedMessages.AsNoTracking().Select(message => message.HeadersJson.Length).ToListAsync();
        var titleLengths = await db.FailedMessageGroups.AsNoTracking().Select(group => group.Title.Length).ToListAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Copied, Is.EqualTo(1));
            Assert.That(headersJsonLengths, Has.Exactly(1).LessThan(4_000).And.Exactly(1).GreaterThan(4_000), "one HeadersJson must fit nvarchar(4000) and the other must not. If the long one is exactly 4000, the insert cut it short");
            Assert.That(titleLengths, Does.Contain(5_000), "the long group Title must be stored whole");
            Assert.That(await PlanUseCountsFor<FailedMessageEntity>(db), Is.EqualTo(new[] { 2 }), "short and long text must reuse one plan, because each new parameter declaration list compiles its own");
            Assert.That(await PlanUseCountsFor<FailedMessageGroupEntity>(db), Is.EqualTo(new[] { 2 }), "the group Title column is unbounded too");
        }
    }

    // Scoped to this test's schema, because the suite's other fixtures share the server and its plan cache.
    static Task<List<int>> PlanUseCountsFor<TEntity>(ServiceControlDbContext db) where TEntity : class
    {
        var statement = $"MERGE {MigrationInsert<TEntity>.For(db).Table} ";
        return db.Database.SqlQuery<int>($"""
            SELECT p.usecounts AS [Value]
            FROM sys.dm_exec_cached_plans AS p
            CROSS APPLY sys.dm_exec_sql_text(p.plan_handle) AS t
            WHERE t.dbid = DB_ID() AND CHARINDEX({statement}, t.text) > 0
            """).ToListAsync();
    }

    static MigrationBatch BatchOf(FailedMessage document)
    {
        var row = new MigrationRow($"FailedMessages/{document.UniqueMessageId}", document, new Dictionary<string, object>
        {
            [MigrationMetadataKeys.StatusChangedAt] = new DateTime(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc),
            [MigrationMetadataKeys.LastModified] = new DateTime(2026, 9, 2, 8, 0, 0, DateTimeKind.Utc)
        }, new MigrationBody("<order>1</order>"u8.ToArray(), "text/xml"));

        return new MigrationBatch([row], row.SourceId);
    }

    static MigrationCheckpoint Checkpoint() =>
        new(MigrationCategoryIds.UnresolvedAndRetryIssuedFailedMessages, MigrationCategoryState.InProgress, null, 0, 0, null, null, null, null, null, null);
}
