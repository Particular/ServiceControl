namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using NServiceBus.CustomChecks;
    using NUnit.Framework;
    using ServiceControl.Audit.Persistence.EFCore.DbContexts;
    using ServiceControl.Audit.Persistence.EFCore.Implementation;
    using ServiceControl.Audit.Persistence.EFCore.Infrastructure;

    class EFRetentionCustomCheckTests : EFPersistenceTestFixture
    {
        AuditRetentionCustomCheck Check => ServiceProvider.GetServices<ICustomCheck>().OfType<AuditRetentionCustomCheck>().Single();

        [Test]
        public async Task Check_fails_once_three_consecutive_sweeps_have_failed()
        {
            var retention = CreateRetentionWithUnreachableDatabase();

            Assert.ThrowsAsync<InvalidOperationException>(() => retention.SweepNow());
            Assert.ThrowsAsync<InvalidOperationException>(() => retention.SweepNow());
            var afterTwo = await Check.PerformCheck();
            Assert.ThrowsAsync<InvalidOperationException>(() => retention.SweepNow());
            var afterThree = await Check.PerformCheck();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(afterTwo.HasFailed, Is.False);
                Assert.That(afterThree.HasFailed, Is.True);
                Assert.That(afterThree.FailureReason, Does.Contain(UnreachableRetentionLock.Reason));
            }
        }

        [Test]
        public async Task Check_fails_once_three_consecutive_sweeps_have_left_expired_rows_behind()
        {
            var retention = ActivatorUtilities.CreateInstance<AuditRetention>(ServiceProvider, new BusyPartitions());

            await retention.SweepNow();
            await retention.SweepNow();
            var afterTwo = await Check.PerformCheck();
            await retention.SweepNow();
            var afterThree = await Check.PerformCheck();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(afterTwo.HasFailed, Is.False);
                Assert.That(afterThree.HasFailed, Is.True);
                Assert.That(afterThree.FailureReason, Does.Contain("too busy"));
            }
        }

        [Test]
        public async Task A_successful_sweep_clears_the_failure()
        {
            FailThreeSweeps();

            await configuration.CreateRetention().SweepNow();

            Assert.That((await Check.PerformCheck()).HasFailed, Is.False);
        }

        [Test]
        public async Task A_sweep_skipped_for_the_lock_leaves_the_failure_in_place()
        {
            FailThreeSweeps();

            await using (await ServiceProvider.GetRequiredService<IRetentionLock>().TryAcquire())
            {
                await configuration.CreateRetention().SweepNow();
            }

            Assert.That((await Check.PerformCheck()).HasFailed, Is.True);
        }

        void FailThreeSweeps()
        {
            var retention = CreateRetentionWithUnreachableDatabase();

            for (var sweep = 0; sweep < 3; sweep++)
            {
                Assert.ThrowsAsync<InvalidOperationException>(() => retention.SweepNow());
            }
        }

        AuditRetention CreateRetentionWithUnreachableDatabase() =>
            ActivatorUtilities.CreateInstance<AuditRetention>(ServiceProvider, new UnreachableRetentionLock());

        sealed class UnreachableRetentionLock : IRetentionLock
        {
            public const string Reason = "The database is unreachable";

            public Task<IAsyncDisposable> TryAcquire(CancellationToken cancellationToken = default) =>
                Task.FromException<IAsyncDisposable>(new InvalidOperationException(Reason));
        }

        // Has one expired day that can never be dropped, like a partition another session keeps locked.
        sealed class BusyPartitions : IAuditPartitionManager
        {
            static readonly DateTime ExpiredDay = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            public Task EnsurePartitions(AuditDbContext dbContext, DateTime from, DateTime toExclusive, CancellationToken cancellationToken = default) => Task.CompletedTask;

            public Task<IReadOnlyList<DateTime>> ListExpired(AuditDbContext dbContext, DateTime keepFrom, CancellationToken cancellationToken = default) =>
                Task.FromResult<IReadOnlyList<DateTime>>([ExpiredDay]);

            public Task<DropResult> DropExpired(AuditDbContext dbContext, DateTime start, int batchSize, CancellationToken cancellationToken = default) =>
                Task.FromResult(new DropResult(RowsDeleted: 0, Completed: true, Deferred: true));

            public Task<DateTime?> ProvisionedUntil(AuditDbContext dbContext, CancellationToken cancellationToken = default) =>
                Task.FromResult<DateTime?>(null);
        }
    }
}
