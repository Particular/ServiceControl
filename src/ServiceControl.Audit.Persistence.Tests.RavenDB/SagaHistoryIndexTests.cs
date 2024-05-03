namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using Raven.Client.Documents;
    using ServiceControl.SagaAudit;

    [TestFixture]
    class SagaHistoryIndexTests : PersistenceTestFixture
    {
        [Test]
        public async Task Should_only_index_saga_id_property()
        {
            var sagaType = "MySagaType";
            var sagaState = "some-saga-state";

            await IngestSagaAudits(new SagaSnapshot
            {
                SagaId = Guid.NewGuid(),
                SagaType = sagaType,
                Status = SagaStateChangeStatus.New,
                StateAfterChange = sagaState
            });

            await configuration.CompleteDBOperation();

            using (var session = configuration.DocumentStore.OpenAsyncSession())
            {
                var sagaTypeResults = await session.Query<SagaHistory, SagaDetailsIndex>()
                    .Search(s => s.SagaType, sagaType)
                    .ToListAsync();

                Assert.AreEqual(0, sagaTypeResults.Count);

                var sagaChangeResults = await session.Query<SagaHistory, SagaDetailsIndex>()
                    .Search(s => s.Changes, sagaState)
                    .ToListAsync();

                Assert.AreEqual(0, sagaChangeResults.Count);
            }
        }

        async Task IngestSagaAudits(params SagaSnapshot[] snapshots)
        {
            var unitOfWork = await StartAuditUnitOfWork(snapshots.Length);
            foreach (var snapshot in snapshots)
            {
                await unitOfWork.RecordSagaSnapshot(snapshot);
            }
            await unitOfWork.DisposeAsync();
            await configuration.CompleteDBOperation();
        }
    }
}