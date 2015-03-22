namespace Particular.Backend.Debugging.RavenDB.Api
{
    using System;
    using System.Linq;
    using Particular.Backend.Debugging.Api;
    using Particular.Backend.Debugging.RavenDB.Model;
    using Raven.Client;
    using Raven.Client.Indexes;

    public class SagaSnapshotIndex : AbstractIndexCreationTask<SagaSnapshot>
    {
        public SagaSnapshotIndex()
        {
            Map = docs => from doc in docs
                select new
                       {
                          doc.SagaId,
                          doc.FinishTime
                       };
            DisableInMemoryIndexing = true;
        }

        public static bool TryGetSagaHistory(IDocumentSession session, Guid sagaId, out SagaHistory sagaHistory, out DateTime lastModified)
        {
            var sagaSnapshots = session.Query<SagaSnapshot, SagaSnapshotIndex>()
                .OrderByDescending(x=>x.FinishTime)
                .Where(x => x.SagaId == sagaId)
                .ToList();
            if (!sagaSnapshots.Any())
            {
                sagaHistory = null;
                lastModified = DateTime.MinValue;
                return false;
            }


            var first = sagaSnapshots.First();
            sagaHistory = new SagaHistory
                          {
                              Id = first.SagaId,
                              SagaId = first.SagaId,
                              SagaType = first.SagaType,
                              Changes = sagaSnapshots
                                  .Select(x => new SagaStateChange
                                               {
                                                   StartTime = x.StartTime,
                                                   FinishTime = x.FinishTime,
                                                   Status = x.Status,
                                                   StateAfterChange = x.StateAfterChange,
                                                   InitiatingMessage = x.InitiatingMessage,
                                                   OutgoingMessages = x.OutgoingMessages,
                                                   Endpoint = x.Endpoint,
                                               })
                                  .ToList()
                          };
            lastModified = sagaHistory.Changes.OrderByDescending(x => x.FinishTime)
                      .Select(y => y.FinishTime)
                      .First();
            return true;
        }
    }
}