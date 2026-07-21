namespace ServiceControl.Persistence.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.DependencyInjection;
    using NUnit.Framework;
    using ServiceControl.Persistence.EFCore.DbContexts;
    using ServiceControl.Persistence.EFCore.Entities;
    using ServiceControl.Persistence.EFCore.PostgreSql.Infrastructure;

    [TestFixture]
    class KnownEndpointsReconcilerTests : PersistenceTestBase
    {
        [Test]
        public async Task Moves_pending_rows_into_known_endpoints()
        {
            var row1 = NewInsertOnlyRow("Endpoint1");
            var row2 = NewInsertOnlyRow("Endpoint2");
            await SeedInsertOnlyRows(row1, row2);

            await RunReconcileBatch();

            var ids = new[] { row1.KnownEndpointId, row2.KnownEndpointId };
            var knownEndpoints = await GetKnownEndpoints(ids);
            Assert.That(knownEndpoints, Has.Count.EqualTo(2));

            var endpoint1 = knownEndpoints.Single(e => e.Id == row1.KnownEndpointId);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(endpoint1.Name, Is.EqualTo(row1.Name));
                Assert.That(endpoint1.HostId, Is.EqualTo(row1.HostId));
                Assert.That(endpoint1.Host, Is.EqualTo(row1.Host));
                Assert.That(endpoint1.Monitored, Is.False, "Endpoints detected during ingestion should not be monitored by default");
            }

            Assert.That(await CountInsertOnlyRows(ids), Is.Zero, "Reconciled rows should be removed from the insert-only table");
        }

        [Test]
        public async Task Duplicate_pending_rows_produce_single_endpoint()
        {
            var row = NewInsertOnlyRow("Endpoint1");
            var duplicate = NewInsertOnlyRow("Endpoint1");
            duplicate.KnownEndpointId = row.KnownEndpointId;
            await SeedInsertOnlyRows(row, duplicate);

            await RunReconcileBatch();

            var ids = new[] { row.KnownEndpointId };
            using (Assert.EnterMultipleScope())
            {
                Assert.That(await GetKnownEndpoints(ids), Has.Count.EqualTo(1));
                Assert.That(await CountInsertOnlyRows(ids), Is.Zero);
            }
        }

        [Test]
        public async Task Existing_endpoints_are_not_modified()
        {
            var row = NewInsertOnlyRow("Endpoint1");
            await SeedKnownEndpoint(new KnownEndpointEntity
            {
                Id = row.KnownEndpointId,
                Name = row.Name,
                HostId = row.HostId,
                Host = row.Host,
                Monitored = true
            });
            await SeedInsertOnlyRows(row);

            await RunReconcileBatch();

            var ids = new[] { row.KnownEndpointId };
            var knownEndpoints = await GetKnownEndpoints(ids);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(knownEndpoints, Has.Count.EqualTo(1));
                Assert.That(knownEndpoints[0].Monitored, Is.True, "Reconciliation should not overwrite existing endpoints");
                Assert.That(await CountInsertOnlyRows(ids), Is.Zero);
            }
        }

        [Test]
        public async Task Reconciles_in_batches()
        {
            var rows = new[] { NewInsertOnlyRow("Endpoint1"), NewInsertOnlyRow("Endpoint2"), NewInsertOnlyRow("Endpoint3") };
            await SeedInsertOnlyRows(rows);
            var ids = rows.Select(r => r.KnownEndpointId).ToArray();

            await RunReconcileBatch(batchSize: 2);
            Assert.That(await CountInsertOnlyRows(ids), Is.EqualTo(1), "Only one batch should be processed per call");

            await RunReconcileBatch(batchSize: 2);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(await CountInsertOnlyRows(ids), Is.Zero);
                Assert.That(await GetKnownEndpoints(ids), Has.Count.EqualTo(3));
            }
        }

        static KnownEndpointInsertOnlyEntity NewInsertOnlyRow(string name) => new()
        {
            KnownEndpointId = Guid.NewGuid(),
            Name = name,
            HostId = Guid.NewGuid(),
            Host = "Host1"
        };

        async Task SeedInsertOnlyRows(params KnownEndpointInsertOnlyEntity[] rows)
        {
            using var scope = ServiceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();
            dbContext.KnownEndpointsInsertOnly.AddRange(rows);
            await dbContext.SaveChangesAsync();
        }

        async Task SeedKnownEndpoint(KnownEndpointEntity entity)
        {
            using var scope = ServiceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();
            dbContext.KnownEndpoints.Add(entity);
            await dbContext.SaveChangesAsync();
        }

        async Task RunReconcileBatch(int batchSize = 1000)
        {
            using var scope = ServiceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();
            var strategy = dbContext.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await dbContext.Database.BeginTransactionAsync();
                await KnownEndpointsReconciler.ReconcileBatch(dbContext, batchSize, CancellationToken.None);
                await transaction.CommitAsync();
            });
        }

        async Task<List<KnownEndpointEntity>> GetKnownEndpoints(IReadOnlyCollection<Guid> ids)
        {
            using var scope = ServiceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();
            return await dbContext.KnownEndpoints.AsNoTracking().Where(e => ids.Contains(e.Id)).ToListAsync();
        }

        async Task<int> CountInsertOnlyRows(IReadOnlyCollection<Guid> ids)
        {
            using var scope = ServiceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();
            return await dbContext.KnownEndpointsInsertOnly.CountAsync(e => ids.Contains(e.KnownEndpointId));
        }
    }
}
