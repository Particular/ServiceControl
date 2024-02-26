namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using Raven.Client.Documents.Indexes;
    using Raven.Client.Documents.Operations.Indexes;
    using ServiceControl.SagaAudit;

    [TestFixture]
    class SagaDetailsIndexTests : PersistenceTestFixture
    {
        [Test]
        public async Task Deletes_index_that_does_not_have_cap_of_50000()
        {
            await configuration.DocumentStore.Maintenance.SendAsync(new DeleteIndexOperation("SagaDetailsIndex"));

            var indexWithout50000capDefinition = new IndexDefinition
            {
                Name = "SagaDetailsIndex",
                Maps =
                            [
                                @"from doc in docs
                                                     select new
                                                     {
                                                         doc.SagaId,
                                                         Id = doc.SagaId,
                                                         doc.SagaType,
                                                         Changes = new[]
                                                         {
                                    new
                                    {
                                        Endpoint = doc.Endpoint,
                                        FinishTime = doc.FinishTime,
                                        InitiatingMessage = doc.InitiatingMessage,
                                        OutgoingMessages = doc.OutgoingMessages,
                                        StartTime = doc.StartTime,
                                        StateAfterChange = doc.StateAfterChange,
                                        Status = doc.Status
                                    }
                                }
            }"
                            ],
                Reduce = @"from result in results
                                            group result by result.SagaId
                            into g
                                            let first = g.First()
                                            select new
                                            {
                                                Id = first.SagaId,
                                                SagaId = first.SagaId,
                                                SagaType = first.SagaType,
                                                Changes = g.SelectMany(x => x.Changes)
                                                    .OrderByDescending(x => x.FinishTime)
                                                    .ToList()
                                            }"
            };

            var putIndexesOp = new PutIndexesOperation(indexWithout50000capDefinition);

            await configuration.DocumentStore.Maintenance.SendAsync(putIndexesOp);

            var sagaDetailsIndexOperation = new GetIndexOperation("SagaDetailsIndex");
            var sagaDetailsIndexDefinition = await configuration.DocumentStore.Maintenance.SendAsync(sagaDetailsIndexOperation);

            Assert.IsNotNull(sagaDetailsIndexDefinition);

            await Persistence.RavenDB.DatabaseSetup.DeleteLegacySagaDetailsIndex(configuration.DocumentStore, CancellationToken.None);

            sagaDetailsIndexDefinition = await configuration.DocumentStore.Maintenance.SendAsync(sagaDetailsIndexOperation);

            Assert.IsNull(sagaDetailsIndexDefinition);
        }

        [Test]
        public async Task Does_not_delete_index_that_does_have_cap_of_50000()
        {
            await Persistence.RavenDB.DatabaseSetup.DeleteLegacySagaDetailsIndex(configuration.DocumentStore, CancellationToken.None);

            var sagaDetailsIndexOperation = new GetIndexOperation("SagaDetailsIndex");
            var sagaDetailsIndexDefinition = await configuration.DocumentStore.Maintenance.SendAsync(sagaDetailsIndexOperation);

            Assert.IsNotNull(sagaDetailsIndexDefinition);
        }

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

            using (var session = configuration.DocumentStore.OpenAsyncSession())
            {
                var sagaDetailsIndexOperation = new GetIndexOperation("SagaDetailsIndex");
                var sagaDetailsIndexDefinition = await configuration.DocumentStore.Maintenance.SendAsync(sagaDetailsIndexOperation);

                Assert.IsTrue(sagaDetailsIndexDefinition.Reduce.Contains("Take(50000)"), "The SagaDetails index definition does not contain a .Take(50000) to limit the number of saga state changes that are reduced by the map/reduce");
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