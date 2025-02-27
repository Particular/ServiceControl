namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using Persistence.RavenDB;
    using Raven.Client.Documents.Indexes;
    using Raven.Client.Documents.Operations.Indexes;
    using ServiceControl.SagaAudit;

    [TestFixture]
    class SagaDetailsIndexTests : PersistenceTestFixture
    {
        [Test]
        public async Task Deletes_index_that_does_not_have_cap_of_50000()
        {
            await configuration.DocumentStore.Maintenance.SendAsync(new DeleteIndexOperation(DatabaseSetup.SagaDetailsIndexName));

            var indexWithout50000capDefinition = new IndexDefinition
            {
                Name = DatabaseSetup.SagaDetailsIndexName,
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

            var sagaDetailsIndexOperation = new GetIndexOperation(DatabaseSetup.SagaDetailsIndexName);
            var sagaDetailsIndexDefinition = await configuration.DocumentStore.Maintenance.SendAsync(sagaDetailsIndexOperation);

            Assert.That(sagaDetailsIndexDefinition, Is.Not.Null);

            await DatabaseSetup.DeleteLegacySagaDetailsIndex(configuration.DocumentStore, CancellationToken.None);

            sagaDetailsIndexDefinition = await configuration.DocumentStore.Maintenance.SendAsync(sagaDetailsIndexOperation);

            Assert.That(sagaDetailsIndexDefinition, Is.Null);
        }

        [Test]
        public async Task Does_not_delete_index_that_does_have_cap_of_50000()
        {
            await DatabaseSetup.DeleteLegacySagaDetailsIndex(configuration.DocumentStore, CancellationToken.None);

            var sagaDetailsIndexOperation = new GetIndexOperation(DatabaseSetup.SagaDetailsIndexName);
            var sagaDetailsIndexDefinition = await configuration.DocumentStore.Maintenance.SendAsync(sagaDetailsIndexOperation);

            Assert.That(sagaDetailsIndexDefinition, Is.Not.Null);
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

            var sagaDetailsIndexOperation = new GetIndexOperation(DatabaseSetup.SagaDetailsIndexName);
            var sagaDetailsIndexDefinition = await configuration.DocumentStore.Maintenance.SendAsync(sagaDetailsIndexOperation);

            Assert.That(sagaDetailsIndexDefinition.Reduce, Does.Contain("Take(50000)"), "The SagaDetails index definition does not contain a .Take(50000) to limit the number of saga state changes that are reduced by the map/reduce");
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