namespace ServiceControl.UnitTests.SagaAudit
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using NUnit.Framework;
    using Particular.Approvals;
    using ServiceControl.SagaAudit;

    [TestFixture]
    class SagaDetailsIndexTests
    {
        [Test]
        public void RunMapReduce()
        {
            using (var store = InMemoryStoreBuilder.GetInMemoryStore())
            {
                store.ExecuteIndex(new SagaDetailsIndex());
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
                    var mapReduceResults = session.Query<SagaHistory, SagaDetailsIndex>()
                        .ToList();
                    Approver.Verify(mapReduceResults);
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
            yield return new SagaHistory
            {
                SagaId = Guid.Empty,
                SagaType = "MySaga1",
                Changes = new List<SagaStateChange>
                {
                    new SagaStateChange
                    {
                        Endpoint = "MyEndpoint",
                        FinishTime = new DateTime(2000, 1, 1, 1, 1, 1, DateTimeKind.Utc),
                        Status = SagaStateChangeStatus.Updated,
                        StartTime = new DateTime(2000, 1, 1, 1, 1, 1, DateTimeKind.Utc),
                        StateAfterChange = "Started"
                    }
                }
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
}