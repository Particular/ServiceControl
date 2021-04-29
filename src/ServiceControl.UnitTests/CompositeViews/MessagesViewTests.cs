namespace ServiceControl.UnitTests.CompositeViews
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Contracts.Operations;
    using MessageAuditing;
    using MessageFailures;
    using NServiceBus;
    using NUnit.Framework;
    using Raven.Client;
    using Raven.Client.Linq;
    using ServiceControl.CompositeViews.Messages;
    using ServiceControl.SagaAudit;

    [TestFixture]
    public class MessagesViewTests
    {
        [Test]
        public void Filter_out_system_messages()
        {
            using (var session = documentStore.OpenSession())
            {
                var processedMessage = new ProcessedMessage
                {
                    Id = "1"
                };

                processedMessage.MakeSystemMessage();
                session.Store(processedMessage);

                var processedMessage2 = new ProcessedMessage
                {
                    Id = "2"
                };
                processedMessage2.MakeSystemMessage(false);
                session.Store(processedMessage2);

                session.SaveChanges();
            }

            documentStore.WaitForIndexing();

            using (var session = documentStore.OpenSession())
            {
                var results = session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                    .Customize(x => x.WaitForNonStaleResults())
                    .Where(x => !x.IsSystemMessage)
                    .OfType<ProcessedMessage>()
                    .ToList();
                Assert.AreEqual(1, results.Count);
                Assert.AreNotEqual("1", results.Single().Id);
            }
        }

        [Test]
        public void Order_by_critical_time()
        {
            using (var session = documentStore.OpenSession())
            {
                session.Store(new ProcessedMessage
                {
                    Id = "1",
                    MessageMetadata = new Dictionary<string, object> { { "CriticalTime", TimeSpan.FromSeconds(10) } }
                });

                session.Store(new ProcessedMessage
                {
                    Id = "2",
                    MessageMetadata = new Dictionary<string, object> { { "CriticalTime", TimeSpan.FromSeconds(20) } }
                });

                session.Store(new ProcessedMessage
                {
                    Id = "3",
                    MessageMetadata = new Dictionary<string, object> { { "CriticalTime", TimeSpan.FromSeconds(15) } }
                });

                session.Store(new FailedMessage
                {
                    Id = "4",
                    Status = FailedMessageStatus.Unresolved,
                    ProcessingAttempts = new List<FailedMessage.ProcessingAttempt>
                    {
                        new FailedMessage.ProcessingAttempt {MessageMetadata = new Dictionary<string, object> {{"CriticalTime", TimeSpan.FromSeconds(15)}}}
                    }
                });
                session.SaveChanges();
            }

            documentStore.WaitForIndexing();

            using (var session = documentStore.OpenSession())
            {
                var firstByCriticalTime = session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                    .OrderBy(x => x.CriticalTime)
                    .Where(x => x.CriticalTime.HasValue)
                    .ProjectFromIndexFieldsInto<ProcessedMessage>()
                    .First();

                Assert.AreEqual("1", firstByCriticalTime.Id);

                var firstByCriticalTimeDescription = session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                    .OrderByDescending(x => x.CriticalTime)
                    .Where(x => x.CriticalTime.HasValue)
                    .ProjectFromIndexFieldsInto<ProcessedMessage>()
                    .First();
                Assert.AreEqual("2", firstByCriticalTimeDescription.Id);
            }
        }

        [Test]
        public void Order_by_time_sent()
        {
            using (var session = documentStore.OpenSession())
            {
                session.Store(new ProcessedMessage
                {
                    Id = "1",
                    MessageMetadata = new Dictionary<string, object> { { "TimeSent", DateTime.Today.AddSeconds(20) } }
                });

                session.Store(new ProcessedMessage
                {
                    Id = "2",
                    MessageMetadata = new Dictionary<string, object> { { "TimeSent", DateTime.Today.AddSeconds(10) } }
                });
                session.Store(new ProcessedMessage
                {
                    Id = "3",
                    MessageMetadata = new Dictionary<string, object> { { "TimeSent", DateTime.Today.AddDays(-1) } }
                });
                session.SaveChanges();
            }

            documentStore.WaitForIndexing();

            using (var session = documentStore.OpenSession())
            {
                var firstByTimeSent = session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                    .OrderBy(x => x.TimeSent)
                    .ProjectFromIndexFieldsInto<ProcessedMessage>()
                    .First();
                Assert.AreEqual("3", firstByTimeSent.Id);

                var firstByTimeSentDescription = session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                    .OrderByDescending(x => x.TimeSent)
                    .ProjectFromIndexFieldsInto<ProcessedMessage>()
                    .First();
                Assert.AreEqual("1", firstByTimeSentDescription.Id);
            }
        }

        [Test]
        public void TimeSent_is_not_cast_to_DateTimeMin_if_null()
        {
            using (var session = documentStore.OpenSession())
            {
                session.Store(new ProcessedMessage
                {
                    MessageMetadata = new Dictionary<string, object>
                    {
                        {"MessageIntent", "1"},
                        {"TimeSent", null}
                    }
                });
                session.Store(new FailedMessage
                {
                    ProcessingAttempts = new List<FailedMessage.ProcessingAttempt>
                    {
                        new FailedMessage.ProcessingAttempt
                        {
                            AttemptedAt = DateTime.Today,
                            MessageMetadata = new Dictionary<string, object>
                            {
                                {"MessageIntent", "1"},
                                {"TimeSent", null}
                            }
                        },
                        new FailedMessage.ProcessingAttempt
                        {
                            AttemptedAt = DateTime.Today,
                            MessageMetadata = new Dictionary<string, object>
                            {
                                {"MessageIntent", "1"},
                                {"TimeSent", null}
                            }
                        }
                    }
                });

                session.SaveChanges();
            }

            documentStore.WaitForIndexing();

            using (var session = documentStore.OpenSession())
            {
                var messageWithNoTimeSent = session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                    .TransformWith<MessagesViewTransformer, MessagesView>()
                    .Customize(x => x.WaitForNonStaleResults())
                    .ToArray();
                Assert.AreEqual(null, messageWithNoTimeSent[0].TimeSent);
                Assert.AreEqual(null, messageWithNoTimeSent[1].TimeSent);
            }
        }

        [Test]
        public void Correct_status_for_repeated_errors()
        {
            using (var session = documentStore.OpenSession())
            {
                session.Store(new FailedMessage
                {
                    Id = "1",
                    ProcessingAttempts = new List<FailedMessage.ProcessingAttempt>
                    {
                        new FailedMessage.ProcessingAttempt
                        {
                            AttemptedAt = DateTime.Today,
                            MessageMetadata = new Dictionary<string, object> {{"MessageIntent", "1"}}
                        },
                        new FailedMessage.ProcessingAttempt
                        {
                            AttemptedAt = DateTime.Today,
                            MessageMetadata = new Dictionary<string, object> {{"MessageIntent", "1"}}
                        }
                    }
                });

                session.SaveChanges();
            }

            documentStore.WaitForIndexing();

            using (var session = documentStore.OpenSession())
            {
                var message = session.Query<FailedMessage>()
                    .TransformWith<MessagesViewTransformer, MessagesView>()
                    .Customize(x => x.WaitForNonStaleResults())
                    .Single();

                Assert.AreEqual(MessageStatus.RepeatedFailure, message.Status);
            }
        }

        [Test]
        public void Check_if_diagnostic_headers_are_present()
        {
            using (var session = documentStore.OpenSession())
            {
                session.Store(new ProcessedMessage
                {
                    Id = "1",
                    MessageMetadata = new Dictionary<string, object> {
                        { "CriticalTime", TimeSpan.FromSeconds(10) },
                        { "MessageId", "1" },
                        { "MessageType", ""},
                        { "SendingEndpoint", ""},
                        { "ReceivingEndpoint", new EndpointDetails{ Name = "ReceivingEndpoint"} },
                        { "TimeSent", DateTime.UtcNow},
                        { "ProcessingTime", TimeSpan.FromSeconds(10)},
                        { "DeliveryTime", TimeSpan.FromSeconds(10)},
                        { "IsSystemMessage", false},
                        { "ConversationId", ""},
                        { "MessageIntent", MessageIntentEnum.Send},
                        { "BodyUrl", ""},
                        { "ContentLength", 50},
                        { "InvokedSagas", new List<SagaInfo>()},
                        { "OriginatesFromSaga", ""}
                    },
                    Headers = {
                        {"NServiceBus.MessageId", "caf68027-acce-4260-8442-ac6500e46afc" },
                        {"NServiceBus.MessageIntent", "Publish"},
                        {"NServiceBus.RelatedTo", "2e6c6c9c-c4cb-474e-98b8-ac6500e46a0c"},
                        {"NServiceBus.ConversationId", "37c405c8-e4e4-4873-8eb4-ac6500e46621"},
                        {"NServiceBus.CorrelationId", "6e449174-6f77-4da4-b9c0-ac6500e46621"},
                        {"NServiceBus.OriginatingMachine", "MACHINE"},
                        {"NServiceBus.OriginatingEndpoint", "PurchaseOrderService.1.0"},
                        {"$.diagnostics.originating.hostid", "4f8138bdb0421ffe1ceaee86e9145721"},
                        {"NServiceBus.OriginatingSagaId", "9e0d2f01-e903-481a-b272-ac6500e46715" },
                        {"NServiceBus.OriginatingSagaType", "PowerSupplyPurchaseOrderService.PurchaseOrderSaga, PowerSupplyPurchaseOrderService, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null" },
                        {"NServiceBus.ReplyToAddress", "PowerSupplyPurchaseOrderService.1.0@[dbo]@[Market.NServiceBus.Prod]" },
                        {"NServiceBus.ContentType", "application/json" },
                        {"NServiceBus.EnclosedMessageTypes", "PowerSupplyPurchaseOrderService.ApiModels.Events.v1_0.PowerSupplyDebtorBlacklistCheckCompleted, PowerSupplyOrderService.ApiModels, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null" },
                        {"NServiceBus.Version", "7.1.0" },
                        {"NServiceBus.TimeSent", "2020-10-31 13:59:26:479745 Z" },
                        {"NServiceBus.Timeout.RouteExpiredTimeoutTo", "Crm.PowerSupplySalesOrderManager.1.0@[dbo]@[Market.NServiceBus.Prod]" },
                        {"NServiceBus.Timeout.Expire", "2020-10-31 13,59,25,359877 Z" },
                        {"NServiceBus.RelatedToTimeoutId", "2c6aa21f-7e73-4142-de86-08d87432ffe4" },
                        {"NServiceBus.ProcessingMachine", "MACHINE" },
                        {"NServiceBus.ProcessingEndpoint", "SeasNve.Market.Crm.PowerSupplySalesOrderManager.1.0" },
                        {"$.diagnostics.hostid", "8d8fcac767fbd7199024c5cae57adde5" },
                        {"$.diagnostics.hostdisplayname", "MACHINE" },
                        {"ServiceControl.Retry.UniqueMessageId", "a5f6da09-5f3f-5394-c09c-dffbe99c357a" },
                        {"NServiceBus.ProcessingStarted", "2020-11-02 08:07:44:650218 Z" },
                        {"NServiceBus.ProcessingEnded", "2020-11-02 08:07:44:837731 Z" }
                    }
                });

                session.SaveChanges();
            }

            documentStore.WaitForIndexing();

            using (var session = documentStore.OpenSession())
            {
                var result = session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                    .TransformWith<MessagesViewTransformer, MessagesView>()
                    .ToList();

                Assert.AreEqual(25, result[0].Headers.Count());
            }
        }

        [SetUp]
        public void SetUp()
        {
            documentStore = InMemoryStoreBuilder.GetInMemoryStore();

            var customIndex = new MessagesViewIndex();
            customIndex.Execute(documentStore);

            var transformer = new MessagesViewTransformer();

            transformer.Execute(documentStore);
        }

        [TearDown]
        public void TearDown()
        {
            documentStore.Dispose();
        }

        IDocumentStore documentStore;
    }
}