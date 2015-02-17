using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ObjectApproval;
using Raven.Client.Indexes;
using ServiceControl.SagaAudit;

[TestFixture]
class SagaStateMigrationTests
{
    [Test]
    public void RunMigration()
    {
        using (var store = InMemoryStoreBuilder.GetInMemoryStore())
        {
            new RavenDocumentsByEntityName().Execute(store);

            using (var session = store.OpenSession())
            {
                foreach (var sagaHistory in GetFakeHistory())
                {
                    session.Store(sagaHistory);
                }
                session.SaveChanges();
            }

            store.WaitForIndexing();
            bool wasCleanEptyRun;
            var migration = new SagaHistoryMigration(TimeSpan.FromDays(50 * 365), TimeSpan.FromMinutes(5))
            {
                Store = store
            };
            migration.Migrate(out wasCleanEptyRun);
            Assert.IsFalse(wasCleanEptyRun);
            using (var session = store.OpenSession())
            {
                var sagaHistories = session.Query<SagaHistory>()
                    .ToList();
                Assert.IsEmpty(sagaHistories);

                var sagaStateMutations = session.Query<SagaSnapshot>()
                    .ToList();
                foreach (var sagaStateMutation in sagaStateMutations)
                {
                    sagaStateMutation.Id = Guid.Empty;
                }
                ObjectApprover.VerifyWithJson(sagaStateMutations);
            }
        }
    }
    [Test]
    public void SecondRunShouldResultInClean()
    {
        using (var store = InMemoryStoreBuilder.GetInMemoryStore())
        {
            new RavenDocumentsByEntityName().Execute(store);

            using (var session = store.OpenSession())
            {
                foreach (var sagaHistory in GetFakeHistory())
                {
                    session.Store(sagaHistory);
                }
                session.SaveChanges();
            }

            store.WaitForIndexing();
            bool wasCleanEmptyRun;
            var migration = new SagaHistoryMigration(TimeSpan.FromDays(50 * 365), TimeSpan.FromMinutes(5))
            {
                Store = store
            };
            migration.Migrate(out wasCleanEmptyRun);
            Assert.IsFalse(wasCleanEmptyRun);
            migration.Migrate(out wasCleanEmptyRun);
            Assert.IsTrue(wasCleanEmptyRun);
        }
    }

    static IEnumerable<SagaHistory> GetFakeHistory()
    {
        yield return new SagaHistory
        {
            SagaId = Guid.Empty,
            SagaType = "MySaga1",
            Changes = new List<SagaStateChange>
                             {
                                 new SagaStateChange
                                 {
                                     Endpoint = "MyEndpoint",
                                     FinishTime = new DateTime(2000, 1, 1, 15, 0, 0),
                                     Status = SagaStateChangeStatus.Updated,
                                     StartTime = new DateTime(2000, 1, 1, 16, 0, 0),
                                     StateAfterChange = "Completed",
                                     InitiatingMessage = new InitiatingMessage
                                                         {
                                                             Intent = "Send",
                                                             IsSagaTimeoutMessage = false,
                                                             MessageId = "1",
                                                             MessageType = "MyMessage1",
                                                             OriginatingEndpoint = "Endpoint1",
                                                             OriginatingMachine = "Machine1",
                                                             TimeSent = new DateTime(2000, 1, 1, 17, 0, 0)
                                                         },
                                     OutgoingMessages = new List<ResultingMessage>
                                                        {
                                                            new ResultingMessage
                                                            {
                                                                DeliverAt = new DateTime(2000, 1, 1, 18, 0, 0),
                                                                DeliveryDelay = TimeSpan.FromMinutes(2),
                                                                Destination = "Endpoint2",
                                                                Intent = "Send",
                                                                MessageId = "2",
                                                                TimeSent = new DateTime(2000, 1, 1, 19, 0, 0),
                                                                MessageType = "MyMessage2"
                                                            }
                                                        }
                                 }
                             }
        };
        yield return new SagaHistory
        {
            SagaId = Guid.Empty,
            SagaType = "MySaga2",
            Changes = new List<SagaStateChange>
                             {
                                 new SagaStateChange
                                 {
                                     Endpoint = "MyEndpoint",
                                     FinishTime = new DateTime(2000, 1, 1, 15, 0, 0),
                                     Status = SagaStateChangeStatus.Updated,
                                     StartTime = new DateTime(2000, 1, 1, 16, 0, 0),
                                     StateAfterChange = "Completed",
                                     InitiatingMessage = new InitiatingMessage
                                                         {
                                                             Intent = "Send",
                                                             IsSagaTimeoutMessage = false,
                                                             MessageId = "1",
                                                             MessageType = "MyMessage1",
                                                             OriginatingEndpoint = "Endpoint1",
                                                             OriginatingMachine = "Machine1",
                                                             TimeSent = new DateTime(2000, 1, 1, 17, 0, 0)
                                                         },
                                     OutgoingMessages = new List<ResultingMessage>
                                                        {
                                                            new ResultingMessage
                                                            {
                                                                DeliverAt = new DateTime(2000, 1, 1, 18, 0, 0),
                                                                DeliveryDelay = TimeSpan.FromMinutes(2),
                                                                Destination = "Endpoint2",
                                                                Intent = "Send",
                                                                MessageId = "2",
                                                                TimeSent = new DateTime(2000, 1, 1, 19, 0, 0),
                                                                MessageType = "MyMessage2"
                                                            }
                                                        }
                                 }
                             }
        };
    }
    

}