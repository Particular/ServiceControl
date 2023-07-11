namespace ServiceControl.Persistence.InMemory
{
    using System;
    using System.Collections.Concurrent;
    using System.Linq;
    using System.Threading.Tasks;
    using ServiceControl.Persistence.Infrastructure;
    using ServiceControl.SagaAudit;

    class InMemorySagaAuditDataStore : ISagaAuditDataStore
    {
        ConcurrentDictionary<string, SagaSnapshot> snapshotStorage = new ConcurrentDictionary<string, SagaSnapshot>();

        public Task<QueryResult<SagaHistory>> GetSagaById(Guid sagaId)
        {
            var snapshots = snapshotStorage.Values.Where(snapshot => snapshot.SagaId == sagaId);

            var results = from result in snapshots
                          group result by result.SagaId into g
                          let first = g.First()
                          select new SagaHistory
                          {
                              Id = first.SagaId,
                              SagaId = first.SagaId,
                              SagaType = first.SagaType,
                              Changes = (from doc in g
                                         select new SagaStateChange
                                         {
                                             Endpoint = doc.Endpoint,
                                             FinishTime = doc.FinishTime,
                                             InitiatingMessage = doc.InitiatingMessage,
                                             OutgoingMessages = doc.OutgoingMessages,
                                             StartTime = doc.StartTime,
                                             StateAfterChange = doc.StateAfterChange,
                                             Status = doc.Status
                                         })
                                        .OrderByDescending(x => x.FinishTime)
                                        .ToList()
                          };

            var sagaHistory = results.FirstOrDefault();
            if (sagaHistory == null)
            {
                return Task.FromResult(QueryResult<SagaHistory>.Empty());
            }

            return Task.FromResult(new QueryResult<SagaHistory>(sagaHistory, new QueryStatsInfo()));
        }

        public Task StoreSnapshot(SagaSnapshot sagaSnapshot)
        {
            snapshotStorage[sagaSnapshot.Id] = sagaSnapshot;
            return Task.CompletedTask;
        }
    }
}
