namespace ServiceControl.UnitTests.Migrations._1._27
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using NServiceBus;
    using NServiceBus.ObjectBuilder;
    using NUnit.Framework;
    using Particular.ServiceControl.DbMigrations;
    using Raven.Client;
    using ServiceControl.Contracts.Operations;
    using ServiceControl.Infrastructure;
    using ServiceControl.MessageFailures;
    using ServiceControl.Recoverability;

    [TestFixture]
    public class SplitFailedMessageDocumentsMigrationTests
    {
        [Test]
        public void IgnoresNormalRetries()
        {
            // Arrange
            var messageId = Guid.NewGuid().ToString();
            var failedQ = "SomeProcessingEndpoint@SOME-MACHINE";
            var replyToAddress = "SomeSendingEndpoint@SOME-MACHINE";

            var uniqueMessageId = OldUniqueMessageId(messageId, replyToAddress: replyToAddress, failedQ: failedQ);

            using (var session = documentStore.OpenSession())
            {
                var failedMessage = new FailedMessage
                {
                    Id = FailedMessage.MakeDocumentId(uniqueMessageId),
                    UniqueMessageId = uniqueMessageId,
                    ProcessingAttempts = new List<FailedMessage.ProcessingAttempt>
                    {
                        MakeProcessingAttempt(messageId,

                            failedQ: failedQ,
                            replyToAddress: replyToAddress
                        ),
                        MakeProcessingAttempt(messageId,
                            failedQ: failedQ,
                            replyToAddress: replyToAddress,
                            retryId: uniqueMessageId
                        )
                    },
                    Status = FailedMessageStatus.RetryIssued
                };

                session.Store(failedMessage);
                session.SaveChanges();
            }

            // Act
            var migration = CreateMigration();
            migration.Apply(documentStore);

            // Assert
            using (var session = documentStore.OpenSession())
            {
                var failedMessages = session.Query<FailedMessage>().ToArray();

                Assert.AreEqual(1, failedMessages.Length, "There should still be one FailedMessage");
                Assert.AreEqual(2, failedMessages[0].ProcessingAttempts.Count, "The FailedMessage should have 2 ProcessingAttempts");
            }
        }

        [Test]
        public void SplitsMultipleSubscribers()
        {
            // Arrange
            var messageId = Guid.NewGuid().ToString();
            var subscriber1InputQueue = "Subscriber1@SOME-MACHINE";
            var subscriber2InputQueue = "Subscriber2@SOME-MACHINE";
            var replyToAddress = "SomeSendingEndpoint@SOME-MACHINE";

            var uniqueMessageId = OldUniqueMessageId(messageId, replyToAddress: replyToAddress);

            using (var session = documentStore.OpenSession())
            {
                var failedMessage = new FailedMessage
                {
                    Id = FailedMessage.MakeDocumentId(uniqueMessageId),
                    UniqueMessageId = uniqueMessageId,
                    ProcessingAttempts = new List<FailedMessage.ProcessingAttempt>
                    {
                        MakeProcessingAttempt(messageId,
                            failedQ: subscriber1InputQueue,
                            replyToAddress: replyToAddress
                        ),
                        MakeProcessingAttempt(messageId,
                            failedQ: subscriber2InputQueue,
                            replyToAddress: replyToAddress
                        )
                    },
                    Status = FailedMessageStatus.Unresolved
                };

                session.Store(failedMessage);
                session.SaveChanges();
            }

            // Act
            var migration = CreateMigration();
            migration.Apply(documentStore);

            // Assert
            using (var session = documentStore.OpenSession())
            {
                var failedMessages = session.Query<FailedMessage>().ToArray();

                Assert.AreEqual(2, failedMessages.Length, "There should be 2 failed messages");

                Assert.AreEqual(1, failedMessages[0].ProcessingAttempts.Count, "The first failed message should have one processing attempt");
                Assert.AreEqual(1, failedMessages[1].ProcessingAttempts.Count, "The second failed message should have one processing attempt");

                Assert.IsFalse(failedMessages.Any(x => x.UniqueMessageId == uniqueMessageId), "Neither of the split failed messages should have the same unique id as the original");
            }
        }

        [Test]
        public void SplitsMultipleSubscribers_AfterARetry()
        {
            // Arrange
            var messageId = Guid.NewGuid().ToString();
            var subscriber1InputQueue = "Subscriber1@SOME-MACHINE";
            var subscriber2InputQueue = "Subscriber2@SOME-MACHINE";
            var replyToAddress = "SomeSendingEndpoint@SOME-MACHINE";

            var uniqueMessageId = OldUniqueMessageId(messageId, replyToAddress: replyToAddress);

            using (var session = documentStore.OpenSession())
            {
                var failedMessage = new FailedMessage
                {
                    Id = FailedMessage.MakeDocumentId(uniqueMessageId),
                    UniqueMessageId = uniqueMessageId,
                    ProcessingAttempts = new List<FailedMessage.ProcessingAttempt>
                    {
                        MakeProcessingAttempt(messageId,
                            failedQ: subscriber1InputQueue,
                            replyToAddress: replyToAddress
                        ),
                        MakeProcessingAttempt(messageId,
                            failedQ: subscriber2InputQueue,
                            replyToAddress: replyToAddress
                        ),
                        MakeProcessingAttempt(messageId,
                            failedQ: subscriber2InputQueue,
                            replyToAddress: replyToAddress,
                            retryId: uniqueMessageId
                        )
                    },
                    Status = FailedMessageStatus.Resolved
                };

                session.Store(failedMessage);
                session.SaveChanges();
            }

            // Act
            var migration = CreateMigration();
            migration.Apply(documentStore);

            // Assert
            using (var session = documentStore.OpenSession())
            {
                var failedMessages = session.Query<FailedMessage>().ToArray();

                Assert.AreEqual(2, failedMessages.Length, "There should be 2 failed messages");

                var originalFailedMessage = failedMessages.SingleOrDefault(x => x.UniqueMessageId == uniqueMessageId);
                Assert.IsNotNull(originalFailedMessage, "The original failed message should still exist");
                Assert.AreEqual(FailedMessageStatus.Resolved, originalFailedMessage.Status, "The original failed message status should not change");
                Assert.AreEqual(2, originalFailedMessage.ProcessingAttempts.Count, "The original failed message retains the non-split ProcessingAttempts");

                var newFailedMessages = failedMessages.Except(new[] { originalFailedMessage }).ToArray();
                Assert.AreEqual(1, newFailedMessages.Length, "There should be one new failed message");

                var newFailedMessage = newFailedMessages.Single();

                Assert.AreEqual(FailedMessageStatus.Unresolved, newFailedMessage.Status, "New Failed Message should be unresolved");
                Assert.AreEqual(1, newFailedMessage.ProcessingAttempts.Count, "New Failed Message should have one processing attempt");
            }
        }

        [Test]
        public void SplitsMultipleSubscribers_AfterRedirectedRetry()
        {
            // Arrange
            var messageId = Guid.NewGuid().ToString();
            var sourceQueue = "Source";
            var serviceAOldQueue = "Service-A-Old";
            var serviceBQueue = "Service-B";
            var serviceANewQueue = "Service-A-New";

            var uniqueMessageId = OldUniqueMessageId(messageId, replyToAddress: serviceANewQueue, failedQ: serviceAOldQueue);

            using (var session = documentStore.OpenSession())
            {
                var failedMessage = new FailedMessage
                {
                    Id = FailedMessage.MakeDocumentId(uniqueMessageId),
                    UniqueMessageId = uniqueMessageId,
                    ProcessingAttempts = new List<FailedMessage.ProcessingAttempt>
                    {
                        MakeProcessingAttempt(messageId,
                            failedQ: serviceBQueue,
                            replyToAddress: sourceQueue
                        ),
                        MakeProcessingAttempt(messageId,
                            failedQ: serviceAOldQueue,
                            replyToAddress: sourceQueue
                        ),
                        MakeProcessingAttempt(messageId,
                            failedQ: serviceANewQueue,
                            replyToAddress: sourceQueue,
                            retryId: uniqueMessageId
                        )
                    },
                    Status = FailedMessageStatus.RetryIssued
                };

                session.Store(failedMessage);
                session.SaveChanges();
            }

            // Act
            var migration = CreateMigration();
            migration.Apply(documentStore);

            // Assert
            using (var session = documentStore.OpenSession())
            {
                var failedMessages = session.Query<FailedMessage>().ToArray();

                Assert.AreEqual(2, failedMessages.Length, "There should be two FailedMessage. One for Service B failure and second with two failed attempts for Service A.");
                Assert.AreEqual(2, failedMessages[0].ProcessingAttempts.Count, "The FailedMessage for Service A should have 2 ProcessingAttempts");
                Assert.AreEqual(1, failedMessages[1].ProcessingAttempts.Count, "The FailedMessage for Service A should have 1 ProcessingAttempt");
            }
        }

        [Test]
        public void SplitsMultipleSubscribers_ForInFligthRetries()
        {
            // Arrange
            var messageId = Guid.NewGuid().ToString();
            var subscriber1InputQueue = "Subscriber1@SOME-MACHINE";
            var subscriber2InputQueue = "Subscriber2@SOME-MACHINE";
            var replyToAddress = "SomeSendingEndpoint@SOME-MACHINE";

            var uniqueMessageId = OldUniqueMessageId(messageId, replyToAddress: replyToAddress);

            using (var session = documentStore.OpenSession())
            {
                var failedMessage = new FailedMessage
                {
                    Id = FailedMessage.MakeDocumentId(uniqueMessageId),
                    UniqueMessageId = uniqueMessageId,
                    ProcessingAttempts = new List<FailedMessage.ProcessingAttempt>
                    {
                        MakeProcessingAttempt(messageId,
                            failedQ: subscriber1InputQueue,
                            replyToAddress: replyToAddress
                        ),
                        MakeProcessingAttempt(messageId,
                            failedQ: subscriber2InputQueue,
                            replyToAddress: replyToAddress
                        )
                    },
                    Status = FailedMessageStatus.RetryIssued
                };

                session.Store(failedMessage);
                session.SaveChanges();
            }

            // Act
            var migration = CreateMigration();
            migration.Apply(documentStore);

            // Assert
            using (var session = documentStore.OpenSession())
            {
                var failedMessages = session.Query<FailedMessage>().ToArray();

                Assert.AreEqual(2, failedMessages.Length, "There should be 2 failed messages");

                Assert.AreEqual(1, failedMessages[0].ProcessingAttempts.Count, "The first failed message should have one processing attempt");
                Assert.AreEqual(1, failedMessages[1].ProcessingAttempts.Count, "The second failed message should have one processing attempt");

                Assert.IsTrue(failedMessages.Any(x => x.UniqueMessageId == uniqueMessageId), "The original document should be kept intact to properly handle in-flight retries if they fail again.");
            }
        }

        [Test]
        public void IgnoresRetriesThroughRedirects()
        {
            // Arrange
            var messageId = Guid.NewGuid().ToString();
            var failedQ = "SomeProcessingEndpoint@SOME-MACHINE";
            var redirectedQ = "SomeProcessingEndpoint@SOME-OTHER-MACHINE";
            var replyToAddress = "SomeSendingEndpoint@SOME-MACHINE";

            var uniqueMessageId = OldUniqueMessageId(messageId, replyToAddress: replyToAddress, failedQ: failedQ);

            using (var session = documentStore.OpenSession())
            {
                var failedMessage = new FailedMessage
                {
                    Id = FailedMessage.MakeDocumentId(uniqueMessageId),
                    UniqueMessageId = uniqueMessageId,
                    ProcessingAttempts = new List<FailedMessage.ProcessingAttempt>
                    {
                        MakeProcessingAttempt(messageId,
                            failedQ: failedQ,
                            replyToAddress: replyToAddress
                        ),
                        MakeProcessingAttempt(messageId,
                            failedQ: redirectedQ,
                            replyToAddress: replyToAddress,
                            retryId: uniqueMessageId
                        )
                    },
                    Status = FailedMessageStatus.RetryIssued
                };

                session.Store(failedMessage);
                session.SaveChanges();
            }

            // Act
            var migration = CreateMigration();
            migration.Apply(documentStore);

            // Assert
            using (var session = documentStore.OpenSession())
            {
                var failedMessages = session.Query<FailedMessage>().ToArray();

                Assert.AreEqual(1, failedMessages.Length, "There should still be one FailedMessage");
                Assert.AreEqual(2, failedMessages[0].ProcessingAttempts.Count, "The FailedMessage should have 2 ProcessingAttempts");
            }
        }

        [Test]
        public void NewFailedMessagesAreGrouped()
        {
            // Arrange
            var messageId = Guid.NewGuid().ToString();
            var subscriber1InputQueue = "Subscriber1@SOME-MACHINE";
            var subscriber2InputQueue = "Subscriber2@SOME-MACHINE";
            var replyToAddress = "SomeSendingEndpoint@SOME-MACHINE";

            var uniqueMessageId = OldUniqueMessageId(messageId, replyToAddress: replyToAddress);

            using (var session = documentStore.OpenSession())
            {
                var failedMessage = new FailedMessage
                {
                    Id = FailedMessage.MakeDocumentId(uniqueMessageId),
                    UniqueMessageId = uniqueMessageId,
                    ProcessingAttempts = new List<FailedMessage.ProcessingAttempt>
                    {
                        MakeProcessingAttempt(messageId,
                            failedQ: subscriber1InputQueue,
                            replyToAddress: replyToAddress
                        ),
                        MakeProcessingAttempt(messageId,
                            failedQ: subscriber2InputQueue,
                            replyToAddress: replyToAddress
                        )
                    },
                    Status = FailedMessageStatus.RetryIssued
                };

                session.Store(failedMessage);
                session.SaveChanges();
            }

            var failedMessageEnricher = new FakeFailedMessageEnricher();
            builder.Register(failedMessageEnricher);

            // Act
            var migration = CreateMigration();
            migration.Apply(documentStore);

            // Assert
            using (var session = documentStore.OpenSession())
            {
                var failedMessages = session.Query<FailedMessage>().ToArray();

                var newFailedMessage = failedMessages.SingleOrDefault(x => x.UniqueMessageId != uniqueMessageId);
                Assert.IsNotNull(newFailedMessage, "There should have been a new failed message");

                Assert.IsNotNull(newFailedMessage.FailureGroups, "Failure Groups should have been set");
                Assert.IsNotEmpty(newFailedMessage.FailureGroups, "There should be at least one failure group");

                var group = newFailedMessage.FailureGroups.First();

                Assert.IsNotNull(group, "Failure Group should not be null");
                Assert.AreEqual("GroupId", group.Id);
            }
        }

        private string OldUniqueMessageId(string messageId, string processingEndpoint = null, string replyToAddress = null, string failedQ = null)
            => DeterministicGuid.MakeId(messageId, processingEndpoint ?? replyToAddress ?? failedQ).ToString();

        private FailedMessage.ProcessingAttempt MakeProcessingAttempt(
            string messageId,
            string messageType = null,
            string processingEndpoint = null,
            string replyToAddress = null,
            string failedQ = null,
            DateTime? attemptedAt = null,
            string correlationId = null,
            MessageIntentEnum messageIntent = MessageIntentEnum.Publish,
            string retryId = null)
        {
            var attempt = new FailedMessage.ProcessingAttempt
            {
                MessageId = messageId,
                AttemptedAt = attemptedAt ?? DateTime.UtcNow.AddDays(-1),
                CorrelationId = correlationId ?? Guid.NewGuid().ToString(),
                MessageIntent = messageIntent,
                Recoverable = true,
                ReplyToAddress = replyToAddress
            };

            var headers = new Dictionary<string, string>
            {
                [Headers.MessageId] = messageId,
            };

            if (string.IsNullOrWhiteSpace(processingEndpoint) == false)
            {
                headers[Headers.ProcessingEndpoint] = processingEndpoint;
            }

            if (string.IsNullOrWhiteSpace(replyToAddress) == false)
            {
                headers[Headers.ReplyToAddress] = replyToAddress;
            }

            if (string.IsNullOrWhiteSpace(failedQ) == false)
            {
                headers["NServiceBus.FailedQ"] = failedQ;
            }

            if (string.IsNullOrWhiteSpace(retryId) == false)
            {
                headers["ServiceControl.Retry.UniqueMessageId"] = retryId;
            }

            attempt.Headers = headers;

            var metadata = new Dictionary<string, object>
            {
                ["MessageType"] = messageType ?? "Message Type"
            };

            attempt.MessageMetadata = metadata;

            attempt.FailureDetails = new FailureDetails
            {
                TimeOfFailure = attempt.AttemptedAt
            };

            return attempt;
        }

        private SplitFailedMessageDocumentsMigration CreateMigration() => new SplitFailedMessageDocumentsMigration(builder);

        private IDocumentStore documentStore;
        private FakeBuilder builder;

        [SetUp]
        public void Setup()
        {
            documentStore = InMemoryStoreBuilder.GetInMemoryStore();
            builder = new FakeBuilder();
        }

        [TearDown]
        public void Teardown()
        {
            documentStore.Dispose();
        }

        public class FakeBuilder : IBuilder
        {
            private IList<object> registeredItems = new List<object>();

            public void Register<T>(T item) => registeredItems.Add(item);

            public IEnumerable<T> BuildAll<T>()
            {
                return registeredItems.OfType<T>();
            }

            public IEnumerable<object> BuildAll(Type typeToBuild)
            {
                return registeredItems.Where(typeToBuild.IsInstanceOfType);
            }

            public void Dispose()
            {
                throw new NotImplementedException();
            }

            public object Build(Type typeToBuild)
            {
                throw new NotImplementedException();
            }

            public IBuilder CreateChildBuilder()
            {
                throw new NotImplementedException();
            }

            public T Build<T>()
            {
                throw new NotImplementedException();
            }

            public void Release(object instance)
            {
                throw new NotImplementedException();
            }

            public void BuildAndDispatch(Type typeToBuild, Action<object> action)
            {
                throw new NotImplementedException();
            }
        }

        public class FakeFailedMessageEnricher : IFailedMessageEnricher
        {
            public IEnumerable<FailedMessage.FailureGroup> Enrich(string messageType, FailureDetails failureDetails) => new[]
            {
                new FailedMessage.FailureGroup
                {
                    Id = "GroupId",
                    Title = failureDetails.AddressOfFailingEndpoint,
                    Type = "FakeFailedMessageEnricher"
                }
            };
        }
    }
}