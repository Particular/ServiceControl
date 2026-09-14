namespace ServiceControl.Persistence.Tests;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using ServiceControl.Persistence.EFCore.DbContexts;
using ServiceControl.Persistence.EFCore.Entities;
using ServiceControl.Persistence.EFCore.Implementation.Audit;
using ServiceControl.Persistence.EFCore.Infrastructure;

class AuditPartitionProvisioningTests : AuditIngestionTestBase
{
    [TestCase("audit_messages")]
    [TestCase("saga_snapshots")]
    public async Task Setup_provisions_the_hour_before_now_through_the_lookahead(string table)
    {
        var partitions = await Partitions(table);

        var firstHour = IngestionHour.AddHours(-1);
        var lastHour = IngestionHour + AuditHours.Lookahead - TimeSpan.FromHours(1);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(partitions, Has.Count.EqualTo((int)AuditHours.Lookahead.TotalHours + 1));
            Assert.That(partitions, Does.Contain(AuditHours.PartitionName(table, firstHour)));
            Assert.That(partitions, Does.Contain(AuditHours.PartitionName(table, lastHour)));
            Assert.That(partitions, Does.Not.Contain(AuditHours.PartitionName(table, lastHour.AddHours(1))));
        }
    }

    [Test]
    public async Task Provisioning_an_existing_window_again_changes_nothing()
    {
        var before = await Partitions("audit_messages");

        await EnsurePartitions(IngestionHour.AddHours(-1), IngestionHour + AuditHours.Lookahead);

        Assert.That(await Partitions("audit_messages"), Is.EqualTo(before));
    }

    [Test]
    public async Task Provisioning_extends_the_window_without_touching_existing_partitions()
    {
        var end = IngestionHour + AuditHours.Lookahead;

        await EnsurePartitions(end, end.AddHours(2));

        var partitions = await Partitions("audit_messages");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(partitions, Has.Count.EqualTo((int)AuditHours.Lookahead.TotalHours + 3));
            Assert.That(partitions, Does.Contain(AuditHours.PartitionName("audit_messages", end.AddHours(1))));
        }
    }

    [Test]
    public async Task An_ingested_row_lands_in_the_partition_of_its_ingestion_hour()
    {
        var audit = new IngestedAudit();

        await IngestAudit(audit);

        var partition = await Query(dbContext =>
        {
            var sql = "SELECT tableoid::regclass::text AS \"Value\" FROM " + SchemaQualifiedTableName.For<AuditMessageEntity>(dbContext) + " WHERE unique_message_id = {0}";

            return dbContext.Database.SqlQueryRaw<string>(sql, audit.UniqueMessageId).SingleAsync();
        });

        Assert.That(partition, Does.EndWith(AuditHours.PartitionName("audit_messages", IngestionHour)));
    }

    Task EnsurePartitions(DateTime fromHour, DateTime toHourExclusive) =>
        Query(async dbContext =>
        {
            await ServiceProvider.GetRequiredService<IAuditPartitionManager>().EnsurePartitions(dbContext, fromHour, toHourExclusive);
            return true;
        });

    Task<List<string>> Partitions(string table) =>
        Query(dbContext => dbContext.Database
            .SqlQueryRaw<string>("""
                SELECT c.relname AS "Value"
                FROM pg_inherits i
                JOIN pg_class c ON c.oid = i.inhrelid
                JOIN pg_class p ON p.oid = i.inhparent
                JOIN pg_namespace n ON n.oid = p.relnamespace
                WHERE p.relname = {0} AND n.nspname = {1}
                ORDER BY c.relname
                """, table, dbContext.Schema)
            .ToListAsync());
}
