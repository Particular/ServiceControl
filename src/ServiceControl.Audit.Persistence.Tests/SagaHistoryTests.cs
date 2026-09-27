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