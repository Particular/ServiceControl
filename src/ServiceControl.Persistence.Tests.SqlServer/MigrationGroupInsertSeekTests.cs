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

class MigrationGroupInsertSeekTests : PersistenceTestBase
{
    static readonly MigrationCategory FailedMessagesCategory = MigrationCategoryRegistry.All.Single(category => category.Id == MigrationCategoryIds.UnresolvedAndRetryIssuedFailedMessages);

    IMigrationTarget Target => ServiceProvider.GetRequiredService<IMigrationTarget>();

    [SetUp]
    public Task OpenTarget() => Target.Open();

    [Test]
    public async Task The_group_insert_seeks_existing_rows_by_key_while_the_group_table_is_small()
    {
        List<FailedMessage.FailureGroup> sharedGroups =
        [
            .. new[] { "Exception Type and Stack Trace", "Message Type", "Endpoint Address", "Endpoint Instance", "Endpoint Name" }
                .Select(type => new FailedMessage.FailureGroup { Id = Guid.NewGuid().ToString(), Title = $"{type} title", Type = type })
        ];
        var rows = Enumerable.Range(0, 74)
            .Select(_ => new IngestedFailure { Groups = sharedGroups }.ToFailedMessage())
            .Select(document => new MigrationRow($"FailedMessages/{document.UniqueMessageId}", document, new Dictionary<string, object>
            {
                [MigrationMetadataKeys.StatusChangedAt] = new DateTime(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc),
                [MigrationMetadataKeys.LastModified] = new DateTime(2026, 9, 2, 8, 0, 0, DateTimeKind.Utc)
            }, new MigrationBody("<order>1</order>"u8.ToArray(), "text/xml")))
            .ToList();

        var result = await Target.Write(FailedMessagesCategory, new MigrationBatch(rows, rows[^1].SourceId), new MigrationCheckpoint(MigrationCategoryIds.UnresolvedAndRetryIssuedFailedMessages, MigrationCategoryState.InProgress, null, 0, 0, null, null, null, null, null, null));

        using var scope = ServiceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Copied, Is.EqualTo(74));
            Assert.That(await GroupTableReadsInCachedPlan(db), Is.Not.Empty.And.None.Contains("Scan"), "a scan reads every row of an index and HOLDLOCK locks each one, which FORCESEEK prevents");
        }
    }

    // Seek and scan operators both hold an IndexScan element, so the RelOp above it says which one it is.
    static Task<List<string>> GroupTableReadsInCachedPlan(ServiceControlDbContext db)
    {
        var statement = $"MERGE {MigrationInsert<FailedMessageGroupEntity>.For(db).Table} ";
        return db.Database.SqlQuery<string>($"""
            WITH XMLNAMESPACES (DEFAULT 'http://schemas.microsoft.com/sqlserver/2004/07/showplan')
            SELECT o.n.value('../../@PhysicalOp', 'nvarchar(60)') + ' ' + o.n.value('@Index', 'nvarchar(260)') AS [Value]
            FROM sys.dm_exec_cached_plans AS p
            CROSS APPLY sys.dm_exec_sql_text(p.plan_handle) AS t
            CROSS APPLY sys.dm_exec_query_plan(p.plan_handle) AS q
            CROSS APPLY q.query_plan.nodes('//RelOp/IndexScan/Object[@Table="[FailedMessageGroups]"]') AS o(n)
            WHERE t.dbid = DB_ID() AND CHARINDEX({statement}, t.text) > 0
            """).ToListAsync();
    }
}
