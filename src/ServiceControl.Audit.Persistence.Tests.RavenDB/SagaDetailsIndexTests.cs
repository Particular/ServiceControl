namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using ServiceControl.SagaAudit;

    [TestFixture]
    class SagaDetailsIndexTests : PersistenceTestFixture
    {
        [Test]
        public async Task Should_only_reduce_the_last_50000_saga_state_changes()
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

            var sagaDetailsIndexDefinition = configuration.DocumentStore.DatabaseCommands.GetIndex("SagaDetailsIndex");
            Assert.IsTrue(sagaDetailsIndexDefinition.Reduce.Contains("Take(50000)"), "The SagaDetails index definition does not contain a .Take(50000) to limit the number of saga state changes that are reduced by the map/reduce");
        }

        async Task IngestSagaAudits(params SagaSnapshot[] snapshots)
        {
            var unitOfWork = StartAuditUnitOfWork(snapshots.Length);
            foreach (var snapshot in snapshots)
            {
                await unitOfWork.RecordSagaSnapshot(snapshot);
            }
            await unitOfWork.DisposeAsync();
            await configuration.CompleteDBOperation();
        }
    }
}