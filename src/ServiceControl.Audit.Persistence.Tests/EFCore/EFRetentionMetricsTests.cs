namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Metrics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.DependencyInjection;
    using NServiceBus.CustomChecks;
    using NUnit.Framework;
    using ServiceControl.Audit.Persistence.EFCore.DbContexts;
    using ServiceControl.Audit.Persistence.EFCore.Implementation;
    using ServiceControl.Audit.Persistence.EFCore.Infrastructure;
    using ServiceControl.Audit.Persistence.EFCore.Infrastructure.Metrics;
    using ServiceControl.SagaAudit;

    class EFRetentionMetricsTests : EFPersistenceTestFixture
    {
        public override Task Setup()
        {
            SetSettings = settings => settings.AuditRetentionPeriod = TimeSpan.FromDays(1);
            return base.Setup();
        }

        [Test]
        public async Task A_sweep_reports_one_successful_pass_for_each_kind_of_row()
        {
            await Ingest(MakeMessage());
            await IngestSagaSnapshot();
            configuration.TimeProvider.Advance(TimeSpan.FromDays(3));
            using var recorded = Listen();

            await configuration.CreateRetention().SweepNow();

            var messagesLeft = await WithDbContext((dbContext, token) => dbContext.AuditMessages.CountAsync(token));
            var snapshotsLeft = await WithDbContext((dbContext, token) => dbContext.SagaSnapshots.CountAsync(token));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(recorded.CycleResults(AuditRetentionEntity.AuditMessages), Is.EqualTo(new[] { "success" }));
                Assert.That(recorded.CycleResults(AuditRetentionEntity.SagaSnapshots), Is.EqualTo(new[] { "success" }));
                Assert.That(messagesLeft, Is.Zero, "the pass reported success without removing the expired messages");
                Assert.That(snapshotsLeft, Is.Zero, "the pass reported success without removing the expired snapshots");
            }
        }

        [Test]
        public async Task A_successful_sweep_clears_the_failures_a_failed_one_left()
        {
            using var recorded = Listen();
            await ActivatorUtilities.CreateInstance<AuditRetention>(ServiceProvider, new BusyAuditPartitions()).SweepNow();
            var afterFailure = recorded.ConsecutiveFailures(AuditRetentionEntity.AuditMessages);

            await configuration.CreateRetention().SweepNow();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(afterFailure, Is.EqualTo(1));
                Assert.That(recorded.ConsecutiveFailures(AuditRetentionEntity.AuditMessages), Is.Zero);
                Assert.That(recorded.ConsecutiveFailures(AuditRetentionEntity.SagaSnapshots), Is.Zero);
            }
        }

        [TestCase(AuditRetentionEntity.AuditMessages, AuditRetentionEntity.SagaSnapshots)]
        [TestCase(AuditRetentionEntity.SagaSnapshots, AuditRetentionEntity.AuditMessages)]
        public async Task A_failing_pass_does_not_stop_the_other_kind_of_row_from_being_removed(AuditRetentionEntity failing, AuditRetentionEntity other)
        {
            await Ingest(MakeMessage());
            await IngestSagaSnapshot();
            configuration.TimeProvider.Advance(TimeSpan.FromDays(3));
            using var recorded = Listen();
            var partitions = new FailingAuditPartitions(failing, ServiceProvider.GetRequiredService<IAuditPartitionManager>());

            await ActivatorUtilities.CreateInstance<AuditRetention>(ServiceProvider, partitions).SweepNow();

            var messagesLeft = await WithDbContext((dbContext, token) => dbContext.AuditMessages.CountAsync(token));
            var snapshotsLeft = await WithDbContext((dbContext, token) => dbContext.SagaSnapshots.CountAsync(token));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(recorded.CycleResults(failing), Is.EqualTo(new[] { "failed" }));
                Assert.That(recorded.CycleResults(other), Is.EqualTo(new[] { "success" }));
                Assert.That(recorded.ConsecutiveFailures(failing), Is.EqualTo(1));
                Assert.That(recorded.ConsecutiveFailures(other), Is.Zero);
                Assert.That(failing == AuditRetentionEntity.AuditMessages ? messagesLeft : snapshotsLeft, Is.EqualTo(1), "the other pass also removed this table's rows");
                Assert.That(other == AuditRetentionEntity.AuditMessages ? messagesLeft : snapshotsLeft, Is.Zero, "the failing pass stopped the other pass");
            }
        }

        [Test]
        public async Task A_pass_that_leaves_expired_rows_because_its_table_is_busy_is_reported_as_failed()
        {
            using var recorded = Listen();

            await ActivatorUtilities.CreateInstance<AuditRetention>(ServiceProvider, new BusyAuditPartitions()).SweepNow();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(recorded.CycleResults(AuditRetentionEntity.AuditMessages), Is.EqualTo(new[] { "failed" }));
                Assert.That(recorded.CycleResults(AuditRetentionEntity.SagaSnapshots), Is.EqualTo(new[] { "failed" }));
                Assert.That(recorded.ConsecutiveFailures(AuditRetentionEntity.AuditMessages), Is.EqualTo(1));
                Assert.That(recorded.ConsecutiveFailures(AuditRetentionEntity.SagaSnapshots), Is.EqualTo(1));
            }
        }

        [Test]
        public async Task A_sweep_that_fails_before_its_passes_reports_every_pass_as_failed()
        {
            using var recorded = Listen();
            var retention = ActivatorUtilities.CreateInstance<AuditRetention>(ServiceProvider, new UnreachableRetentionLock());

            Assert.ThrowsAsync<InvalidOperationException>(() => retention.SweepNow());

            using (Assert.EnterMultipleScope())
            {
                Assert.That(recorded.CycleResults(AuditRetentionEntity.AuditMessages), Is.EqualTo(new[] { "failed" }));
                Assert.That(recorded.CycleResults(AuditRetentionEntity.SagaSnapshots), Is.EqualTo(new[] { "failed" }));
                Assert.That(recorded.ConsecutiveFailures(AuditRetentionEntity.AuditMessages), Is.EqualTo(1));
                Assert.That(recorded.ConsecutiveFailures(AuditRetentionEntity.SagaSnapshots), Is.EqualTo(1));
            }
        }

        [Test]
        public async Task A_sweep_whose_partition_provisioning_fails_reports_every_pass_as_failed()
        {
            using var recorded = Listen();
            var partitions = new UnprovisionablePartitions(ServiceProvider.GetRequiredService<IAuditPartitionManager>());
            var retention = ActivatorUtilities.CreateInstance<AuditRetention>(ServiceProvider, partitions);

            Assert.ThrowsAsync<InvalidOperationException>(() => retention.SweepNow());

            using (Assert.EnterMultipleScope())
            {
                Assert.That(recorded.CycleResults(AuditRetentionEntity.AuditMessages), Is.EqualTo(new[] { "failed" }));
                Assert.That(recorded.CycleResults(AuditRetentionEntity.SagaSnapshots), Is.EqualTo(new[] { "failed" }));
            }
        }

        [Test]
        public async Task A_failure_after_the_passes_ran_does_not_record_them_again()
        {
            using var recorded = Listen();
            var retention = ActivatorUtilities.CreateInstance<AuditRetention>(ServiceProvider, new LockThatFailsToRelease());

            Assert.ThrowsAsync<InvalidOperationException>(() => retention.SweepNow());

            using (Assert.EnterMultipleScope())
            {
                Assert.That(recorded.CycleResults(AuditRetentionEntity.AuditMessages), Is.EqualTo(new[] { "success" }));
                Assert.That(recorded.CycleResults(AuditRetentionEntity.SagaSnapshots), Is.EqualTo(new[] { "success" }));
            }
        }

        [Test]
        public async Task A_sweep_cut_short_by_shutdown_is_cancelled_rather_than_failed()
        {
            using var recorded = Listen();
            var check = ServiceProvider.GetServices<ICustomCheck>().OfType<AuditRetentionCustomCheck>().Single();
            var partitions = new ShutdownDuringFirstPass(ServiceProvider.GetRequiredService<IAuditPartitionManager>());

            for (var sweep = 0; sweep < 3; sweep++)
            {
                using var shutdown = new CancellationTokenSource();
                partitions.Shutdown = shutdown;
                var retention = ActivatorUtilities.CreateInstance<AuditRetention>(ServiceProvider, partitions);

                Assert.CatchAsync<OperationCanceledException>(() => retention.SweepNow(shutdown.Token));
            }

            var result = await check.PerformCheck();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(recorded.CycleResults(AuditRetentionEntity.AuditMessages), Is.EqualTo(new[] { "cancelled", "cancelled", "cancelled" }));
                Assert.That(recorded.Cycles(AuditRetentionEntity.SagaSnapshots), Is.Empty, "the pass after shutdown still ran");
                Assert.That(recorded.ConsecutiveFailures(AuditRetentionEntity.AuditMessages), Is.Zero);
                Assert.That(result.HasFailed, Is.False, "shutdown counted as a failed sweep");
            }
        }

        [Test]
        public async Task A_sweep_skipped_for_the_lock_reports_nothing()
        {
            await Ingest(MakeMessage());
            configuration.TimeProvider.Advance(TimeSpan.FromDays(3));
            using var recorded = Listen();

            await using (await ServiceProvider.GetRequiredService<IRetentionLock>().TryAcquire())
            {
                await configuration.CreateRetention().SweepNow();
            }

            var whileLocked = recorded.RecordedAnything();
            await configuration.CreateRetention().SweepNow();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(whileLocked, Is.False);
                Assert.That(recorded.RecordedAnything(), Is.True, "the recorder sees nothing at all, so the first assertion proves nothing");
            }
        }

        async Task IngestSagaSnapshot()
        {
            await using var unitOfWork = await StartAuditUnitOfWork(1);
            await unitOfWork.RecordSagaSnapshot(new SagaSnapshot { SagaId = Guid.NewGuid(), Status = SagaStateChangeStatus.New });
            await unitOfWork.Complete();
            await configuration.CompleteDBOperation();
        }

        RecordedAuditRetentionMetrics Listen() => new(ServiceProvider.GetRequiredService<IMeterFactory>());

        sealed class UnreachableRetentionLock : IRetentionLock
        {
            public Task<IAsyncDisposable> TryAcquire(CancellationToken cancellationToken = default) =>
                Task.FromException<IAsyncDisposable>(new InvalidOperationException("The database is unreachable"));
        }

        sealed class LockThatFailsToRelease : IRetentionLock
        {
            public Task<IAsyncDisposable> TryAcquire(CancellationToken cancellationToken = default) => Task.FromResult<IAsyncDisposable>(new Handle());

            sealed class Handle : IAsyncDisposable
            {
                public ValueTask DisposeAsync() => ValueTask.FromException(new InvalidOperationException("The lock connection was dropped"));
            }
        }

        sealed class UnprovisionablePartitions(IAuditPartitionManager inner) : IAuditPartitionManager
        {
            public Task EnsurePartitions(AuditDbContext dbContext, DateTime from, DateTime toExclusive, CancellationToken cancellationToken = default) =>
                Task.FromException(new InvalidOperationException("The partitions could not be created"));

            public Task<IReadOnlyList<DateTime>> ListExpired(AuditDbContext dbContext, AuditRetentionEntity entity, DateTime keepFrom, CancellationToken cancellationToken = default) =>
                inner.ListExpired(dbContext, entity, keepFrom, cancellationToken);

            public Task DeleteBodies(IAuditBodyStoragePersistence bodyStorage, DateTime expired, CancellationToken cancellationToken = default) => Task.CompletedTask;

            public Task<DropResult> DropExpired(AuditDbContext dbContext, AuditRetentionEntity entity, DateTime start, int batchSize, CancellationToken cancellationToken = default) =>
                inner.DropExpired(dbContext, entity, start, batchSize, cancellationToken);

            public Task<DateTime?> ProvisionedUntil(AuditDbContext dbContext, CancellationToken cancellationToken = default) =>
                inner.ProvisionedUntil(dbContext, cancellationToken);
        }

        // Requests shutdown while the first pass lists its rows, the way a host stopping mid-sweep would.
        sealed class ShutdownDuringFirstPass(IAuditPartitionManager inner) : IAuditPartitionManager
        {
            public CancellationTokenSource Shutdown { get; set; }

            public Task EnsurePartitions(AuditDbContext dbContext, DateTime from, DateTime toExclusive, CancellationToken cancellationToken = default) =>
                inner.EnsurePartitions(dbContext, from, toExclusive, cancellationToken);

            public Task<IReadOnlyList<DateTime>> ListExpired(AuditDbContext dbContext, AuditRetentionEntity entity, DateTime keepFrom, CancellationToken cancellationToken = default)
            {
                Shutdown.Cancel();
                return Task.FromCanceled<IReadOnlyList<DateTime>>(cancellationToken);
            }

            public Task DeleteBodies(IAuditBodyStoragePersistence bodyStorage, DateTime expired, CancellationToken cancellationToken = default) => Task.CompletedTask;

            public Task<DropResult> DropExpired(AuditDbContext dbContext, AuditRetentionEntity entity, DateTime start, int batchSize, CancellationToken cancellationToken = default) =>
                inner.DropExpired(dbContext, entity, start, batchSize, cancellationToken);

            public Task<DateTime?> ProvisionedUntil(AuditDbContext dbContext, CancellationToken cancellationToken = default) =>
                inner.ProvisionedUntil(dbContext, cancellationToken);
        }
    }
}
