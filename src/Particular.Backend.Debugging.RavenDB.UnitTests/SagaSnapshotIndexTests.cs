using System;
using System.Collections.Generic;
using NUnit.Framework;
using ObjectApproval;
using Particular.Backend.Debugging.Api;
using Particular.Backend.Debugging.RavenDB.Api;
using Particular.Backend.Debugging.RavenDB.Model;

namespace Particular.Backend.Debugging.RavenDB.UnitTests
{
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
                    DateTime lastModified;
                    SagaSnapshotIndex.TryGetSagaHistory(session, Guid.Empty, out history, out lastModified);
                    Assert.AreEqual(new DateTime(2002, 4, 1, 1, 1, 1, DateTimeKind.Utc),lastModified);
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
                FinishTime = new DateTime(2001, 2, 1, 1, 1, 1, DateTimeKind.Utc),
                Status = SagaStateChangeStatus.Updated,
                StartTime = new DateTime(2001, 1, 1, 1, 1, 1, DateTimeKind.Utc),
                StateAfterChange = "Updated"
            };
            yield return new SagaSnapshot
            {
                SagaId = Guid.Empty,
                SagaType = "MySaga1",
                Endpoint = "MyEndpoint",
                FinishTime = new DateTime(2002, 4, 1, 1, 1, 1, DateTimeKind.Utc),
                Status = SagaStateChangeStatus.Completed,
                StartTime = new DateTime(2002, 3, 1, 1, 1, 1, DateTimeKind.Utc),
                StateAfterChange = "Completed"
            };
        }

    }
}