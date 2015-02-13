using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ObjectApproval;
using ServiceControl.SagaAudit;

[TestFixture]
class SagaListIndexTests
{
    [Test]
    public void RunMapReduce()
    {
        using (var store = InMemoryStoreBuilder.GetInMemoryStore())
        {
            store.ExecuteIndex(new SagaListIndex());
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
                var mapReduceResults = session.Query<SagaListIndex.Result, SagaListIndex>()
                    .ToList();
                ObjectApprover.VerifyWithJson(mapReduceResults);
            }
        }
    }

    static IEnumerable<object> GetFakeHistory()
    {
        yield return new SagaSnapshot
                     {
                         SagaId = new Guid("00000000-0000-0000-0000-000000000003"),
                         SagaType = "MySaga3",
                         FinishTime = new DateTime(2000, 1, 1, 10, 0, 0),
                     };
        yield return new SagaHistory
        {
            SagaId = new Guid("00000000-0000-0000-0000-000000000001"),
                         SagaType = "MySaga1",
                         Changes = new List<SagaStateChange>
                                   {
                                       new SagaStateChange
                                       {
                                           FinishTime = new DateTime(2000, 1, 1, 1, 1, 1, DateTimeKind.Utc),
                                       }
                                   }
                     };
        yield return new SagaSnapshot
                     {
                         SagaId = new Guid("00000000-0000-0000-0000-000000000002"),
                         SagaType = "MySaga2",
                         FinishTime = new DateTime(2000, 1, 1, 15, 0, 0),
                     };
    }

}