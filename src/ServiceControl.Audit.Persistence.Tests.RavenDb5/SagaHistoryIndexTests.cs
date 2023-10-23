namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using Raven.Client.Documents;
    using Raven.Client.Exceptions;
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
                // This test is trying to query on 2 fields that are marked FieldIndexing.No in the index.
                // In RavenDB 5 that would return 0 results, but in RavenDB 6 with Corax it throws an exception.
                // (Not sure what RavenDB 6's behavior with Lucene indexes is.) It could also be argued that this
                // is testing the infrastructure and the entire test should be discarded.

                Assert.ThrowsAsync<RavenException>(async () =>
                {
                    var sagaTypeResults = await session.Query<SagaHistory, SagaDetailsIndex>()
                        .Search(s => s.SagaType, sagaType)
                        .ToListAsync();
                });

                //Assert.AreEqual(0, sagaTypeResults.Count);

                Assert.ThrowsAsync<RavenException>(async () =>
                {
                    var sagaChangeResults = await session.Query<SagaHistory, SagaDetailsIndex>()
                        .Search(s => s.Changes, sagaState)
                        .ToListAsync();
                });

                //Assert.AreEqual(0, sagaChangeResults.Count);
            }
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