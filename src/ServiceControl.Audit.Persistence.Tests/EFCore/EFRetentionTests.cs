namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using NServiceBus;
    using NUnit.Framework;
    using ServiceControl.Audit.Infrastructure;
    using ServiceControl.Audit.Persistence.EFCore.Infrastructure;
    using ServiceControl.SagaAudit;

    class EFRetentionTests : EFPersistenceTestFixture
    {
        public override Task Setup()
        {
            SetSettings = settings => settings.AuditRetentionPeriod = TimeSpan.FromDays(1);
            return base.Setup();
        }

        [Test]
        public async Task Removes_rows_older_than_the_retention_period()
        {
            var expired = MakeMessage();
            await Ingest(expired);

            configuration.TimeProvider.Advance(TimeSpan.FromDays(3));
            var retention = configuration.CreateRetention();
            await retention.SweepNow();

            var live = MakeMessage();
            await Ingest(live);
            await retention.SweepNow();

            var remaining = await MessagesViewStore.GetMessages(true, new PagingInfo(), new SortInfo("time_sent", "desc"));

            Assert.That(remaining.Results.Select(message => message.MessageId), Is.EqualTo(new[] { live.Headers[Headers.MessageId] }));
        }

        [Test]
        public async Task Removes_the_external_bodies_with_the_rows()
        {
            await Ingest(MakeMessage(contentType: "application/octet-stream"), [1, 2, 3]);
            Assert.That(Directory.GetFiles(configuration.BodyStoragePath, "*", SearchOption.AllDirectories), Has.Length.EqualTo(1));

            configuration.TimeProvider.Advance(TimeSpan.FromDays(3));
            await configuration.CreateRetention().SweepNow();

            var remaining = await MessagesViewStore.GetMessages(true, new PagingInfo(), new SortInfo("time_sent", "desc"));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(remaining.Results, Is.Empty);
                Assert.That(Directory.GetFiles(configuration.BodyStoragePath, "*", SearchOption.AllDirectories), Is.Empty);
            }
        }

        [Test]
        public async Task Deletes_the_bodies_of_the_expired_rows()
        {
            var message = MakeMessage(contentType: "application/octet-stream");
            await Ingest(message, [1, 2, 3]);
            var bodyId = (await MessagesViewStore.GetMessages(true, new PagingInfo(), new SortInfo("time_sent", "desc"))).Results.Single().BodyUrl.Split('/')[2];
            AuditBodyId.TryParse(bodyId, out var createdOn, out _);

            configuration.TimeProvider.Advance(TimeSpan.FromDays(3));
            var storage = new RecordingBodyStorage();
            await configuration.CreateRetention(storage).SweepNow();

            Assert.That(storage.DeletedHours.Concat(storage.DeletedDays), Has.Some.Matches<DateTime>(deleted => deleted == createdOn || deleted == createdOn.Date),
                "the hour (SQL Server) or day (PostgreSQL) of the expired rows");
        }

        [Test]
        public async Task Keeps_the_rows_when_deleting_their_bodies_fails()
        {
            await Ingest(MakeMessage(contentType: "application/octet-stream"), [1, 2, 3]);

            configuration.TimeProvider.Advance(TimeSpan.FromDays(3));
            var storage = new RecordingBodyStorage { DeleteFailure = new IOException("storage is down") };

            Assert.ThrowsAsync<IOException>(() => configuration.CreateRetention(storage).SweepNow());

            var remaining = await MessagesViewStore.GetMessages(true, new PagingInfo(), new SortInfo("time_sent", "desc"));
            var body = await MessagesViewStore.GetMessageBody(remaining.Results.Single().BodyUrl.Split('/')[2], TestContext.CurrentContext.CancellationToken);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(remaining.Results, Has.Count.EqualTo(1));
                Assert.That(body.HasContent, Is.True, "the body is still in storage");
            }
        }

        [Test]
        public async Task A_list_version_moves_when_a_sweep_and_an_arrival_leave_the_total_unchanged()
        {
            await Ingest(MakeMessage());
            var before = await MessagesViewStore.GetMessages(true, new PagingInfo(), new SortInfo("time_sent", "desc"));

            configuration.TimeProvider.Advance(TimeSpan.FromDays(3));
            await configuration.CreateRetention().SweepNow();
            var live = MakeMessage();
            await Ingest(live);
            var after = await MessagesViewStore.GetMessages(true, new PagingInfo(), new SortInfo("time_sent", "desc"));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(after.Results.Select(message => message.MessageId), Is.EqualTo(new[] { live.Headers[Headers.MessageId] }), "the sweep did not swap the expired message for the live one");
                Assert.That(after.QueryStats.TotalCount, Is.EqualTo(before.QueryStats.TotalCount), "the total was meant to hold, as it does at the count cap");
            }

            VersionAssert.Moved(before.QueryStats.Version, after.QueryStats.Version, "the total held, so only the rows can move the version");
        }

        [Test]
        public async Task A_saga_version_moves_when_a_sweep_and_an_arrival_leave_the_change_count_unchanged()
        {
            var sagaId = Guid.NewGuid();
            await IngestSagaSnapshot(new SagaSnapshot { SagaId = sagaId, Status = SagaStateChangeStatus.New });
            var before = await SagaHistoryStore.QuerySagaHistoryById(sagaId);

            configuration.TimeProvider.Advance(TimeSpan.FromDays(3));
            await configuration.CreateRetention().SweepNow();
            await IngestSagaSnapshot(new SagaSnapshot { SagaId = sagaId, Status = SagaStateChangeStatus.Updated });
            var after = await SagaHistoryStore.QuerySagaHistoryById(sagaId);

            Assert.That(after.Results.Changes.Select(change => change.Status), Is.EqualTo(new[] { SagaStateChangeStatus.Updated }), "the sweep did not swap the expired change for the new one");
            VersionAssert.Moved(before.QueryStats.Version, after.QueryStats.Version, "the change count held, so only the rows can move the version");
        }

        [Test]
        public async Task Keeps_rows_until_the_retention_period_has_passed()
        {
            await Ingest(MakeMessage());

            configuration.TimeProvider.Advance(TimeSpan.FromDays(1));

            await configuration.CreateRetention().SweepNow();

            var remaining = await MessagesViewStore.GetMessages(true, new PagingInfo(), new SortInfo("time_sent", "desc"));

            Assert.That(remaining.Results, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task Skips_the_sweep_while_another_instance_holds_the_lock()
        {
            await Ingest(MakeMessage());
            configuration.TimeProvider.Advance(TimeSpan.FromDays(3));

            await using (await ServiceProvider.GetRequiredService<IRetentionLock>().TryAcquire())
            {
                await configuration.CreateRetention().SweepNow();
            }

            var remaining = await MessagesViewStore.GetMessages(true, new PagingInfo(), new SortInfo("time_sent", "desc"));

            Assert.That(remaining.Results, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task The_retention_lock_is_released_with_its_handle()
        {
            var retentionLock = ServiceProvider.GetRequiredService<IRetentionLock>();

            var first = await retentionLock.TryAcquire();
            var whileHeld = await retentionLock.TryAcquire();
            await first!.DisposeAsync();
            var afterRelease = await retentionLock.TryAcquire();
            await afterRelease!.DisposeAsync();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(first, Is.Not.Null);
                Assert.That(whileHeld, Is.Null);
                Assert.That(afterRelease, Is.Not.Null);
            }
        }

        async Task IngestSagaSnapshot(SagaSnapshot snapshot)
        {
            await using var unitOfWork = await StartAuditUnitOfWork(1);
            await unitOfWork.RecordSagaSnapshot(snapshot);
            await unitOfWork.Complete();
            await configuration.CompleteDBOperation();
        }
    }
}
