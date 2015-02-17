using System;
using System.Collections.Generic;
using NUnit.Framework;
using ObjectApproval;
using ServiceControl.SagaAudit;

[TestFixture]
class SagaSnapshotIndexTests
{
    [Test]
    public void RunMapReduce()
    {
        using (var store = InMemoryStoreBuilder.GetInMemoryStore())
        {
            store.ExecuteIndex(new SagaSnapshotIndex());
            using (var session = store.OpenSession())
            {
                foreach (var sagaHistory in GetFakeHistory())
                {
                    session.Store(sagaHistory);
                }
                session.SaveChanges();
            }

            store.WaitForIndexing();

            using (var session = store.OpenSession())
            {
                SagaHistory history;
                SagaSnapshotIndex.TryGetSagaHistory(session, Guid.Empty, out history);
                ObjectApprover.VerifyWithJson(history);
            }
        }
    }

    static IEnumerable<object> GetFakeHistory()
    {
        yield return new SagaSnapshot
                     {
                         SagaId = Guid.Empty,
                         SagaType = "MySaga1",
                         Endpoint = "MyEndpoint",
                         FinishTime = new DateTime(2001, 1, 1, 1, 1, 1, DateTimeKind.Utc),
                         Status = SagaStateChangeStatus.Updated,
                         StartTime = new DateTime(2001, 1, 1, 1, 1, 1, DateTimeKind.Utc),
                         StateAfterChange = "Updated"
                     };
        yield return new SagaSnapshot
                     {
                         SagaId = Guid.Empty,
                         SagaType = "MySaga1",
                         Endpoint = "MyEndpoint",
                         FinishTime = new DateTime(2002, 1, 1, 1, 1, 1, DateTimeKind.Utc),
                         Status = SagaStateChangeStatus.Completed,
                         StartTime = new DateTime(2002, 1, 1, 1, 1, 1, DateTimeKind.Utc),
                         StateAfterChange = "Completed"
                     };
    }

}