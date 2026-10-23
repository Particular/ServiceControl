namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using ServiceControl.SagaAudit;

    [TestFixture]
    class SagaHistoryTests : PersistenceTestFixture
    {
        [Test]
        public async Task Basic_Roundtrip()
        {
            var sagaId = Guid.NewGuid();
            await IngestSagaAudits(
                new SagaSnapshot
                {
                    SagaId = sagaId,
                    SagaType = "MySagaType",
                    Status = SagaStateChangeStatus.New
                }
                );

            var queryResult = await SagaHistoryStore.QuerySagaHistoryById(sagaId, TestContext.CurrentContext.CancellationToken);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(queryResult.Results.SagaId, Is.EqualTo(sagaId));
                Assert.That(queryResult.Results.SagaType, Is.EqualTo("MySagaType"));
                Assert.That(queryResult.Results.Changes, Has.Count.EqualTo(1));
            }
            Assert.That(queryResult.Results.Changes[0].Status, Is.EqualTo(SagaStateChangeStatus.New));
        }

        [Test]
        public async Task Handles_no_results_gracefully()
        {
            var nonExistentSagaId = Guid.NewGuid();
            var queryResult = await SagaHistoryStore.QuerySagaHistoryById(nonExistentSagaId, TestContext.CurrentContext.CancellationToken);

            Assert.That(queryResult.Results, Is.Null);
        }

        [Test]
        public async Task Does_not_return_snapshots_for_other_sagas()
        {
            var sagaId = Guid.NewGuid();
            var otherSagaId = Guid.NewGuid();

            await IngestSagaAudits(
                new SagaSnapshot { SagaId = sagaId },
                new SagaSnapshot { SagaId = otherSagaId },
                new SagaSnapshot { SagaId = sagaId }
            );

            var queryResult = await SagaHistoryStore.QuerySagaHistoryById(sagaId, TestContext.CurrentContext.CancellationToken);

            Assert.That(queryResult.Results.Changes, Has.Count.EqualTo(2));
        }

        [Test]
        public async Task A_missing_saga_still_reports_a_version()
        {
            var queryResult = await SagaHistoryStore.QuerySagaHistoryById(Guid.NewGuid(), TestContext.CurrentContext.CancellationToken);

            Assert.That(queryResult.QueryStats.Version.HasValue, Is.True,
                "the query ran and found nothing, and without a version the primary's merged saga history loses its ETag");
        }

        [Test]
        public async Task A_saga_version_moves_when_its_first_change_arrives()
        {
            var sagaId = Guid.NewGuid();
            var missing = await SagaHistoryStore.QuerySagaHistoryById(sagaId, TestContext.CurrentContext.CancellationToken);

            await IngestSagaAudits(new SagaSnapshot { SagaId = sagaId, Status = SagaStateChangeStatus.New });

            var found = await SagaHistoryStore.QuerySagaHistoryById(sagaId, TestContext.CurrentContext.CancellationToken);

            VersionAssert.Moved(missing.QueryStats.Version, found.QueryStats.Version, "a client holding the not-found version would never see the saga");
        }

        [Test]
        public async Task A_saga_version_moves_when_a_change_arrives()
        {
            var sagaId = Guid.NewGuid();
            await IngestSagaAudits(new SagaSnapshot { SagaId = sagaId, Status = SagaStateChangeStatus.New });

            var issued = await SagaHistoryStore.QuerySagaHistoryById(sagaId, TestContext.CurrentContext.CancellationToken);
            var repeated = await SagaHistoryStore.QuerySagaHistoryById(sagaId, TestContext.CurrentContext.CancellationToken);

            await IngestSagaAudits(new SagaSnapshot { SagaId = sagaId, Status = SagaStateChangeStatus.Updated });

            var afterChange = await SagaHistoryStore.QuerySagaHistoryById(sagaId, TestContext.CurrentContext.CancellationToken);

            Assert.That(afterChange.Results.Changes, Has.Count.EqualTo(2), "the query did not see the new change");
            VersionAssert.Matches(issued.QueryStats.Version, repeated.QueryStats.Version, "nothing was written between the two reads");
            VersionAssert.Moved(repeated.QueryStats.Version, afterChange.QueryStats.Version, "a client holding the old version would never see the new change");
        }


        async Task IngestSagaAudits(params SagaSnapshot[] snapshots)
        {
            await using var unitOfWork = await StartAuditUnitOfWork(snapshots.Length);
            foreach (var snapshot in snapshots)
            {
                await unitOfWork.RecordSagaSnapshot(snapshot);
            }
            await unitOfWork.Complete();
            await configuration.CompleteDBOperation();
        }
    }
}