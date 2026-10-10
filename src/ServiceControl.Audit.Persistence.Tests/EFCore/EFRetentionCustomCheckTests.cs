namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using NServiceBus.CustomChecks;
    using NUnit.Framework;
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
    }
}
