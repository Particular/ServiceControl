namespace ServiceControl.UnitTests.Migrations._1._27
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using NServiceBus;
    using NServiceBus.Faults;
    using NUnit.Framework;
    using Particular.ServiceControl.DbMigrations;
    using Raven.Client;
    using Raven.Client.Embedded;
    using ServiceControl.Contracts.Operations;
    using ServiceControl.Infrastructure;
    using ServiceControl.MessageFailures;
    using ServiceControl.Recoverability;
    using FailedMessage = ServiceControl.MessageFailures.FailedMessage;

    [TestFixture]
    public class When_splitting_multisubscriber_failure_attempts
    {

        [Test]
        public void Should_combine_attempts_from_the_same_endpoint_v4()
        {
            // Arrange
            var scenarioInfo = new PreSplitScenario();
            scenarioInfo.AddV4ProcessingAttempt(PreSplitScenario.Subscriber1InputQueue);
            scenarioInfo.AddV4ProcessingAttempt(PreSplitScenario.Subscriber1InputQueue, true);
            scenarioInfo.AddV4ProcessingAttempt(PreSplitScenario.Subscriber2InputQueue);

            using (var session = documentStore.OpenSession())
            {
                session.Store(scenarioInfo.FailedMessage);
                session.SaveChanges();
            }

            // Act
            var migration = CreateMigration();
            migration.Apply(documentStore);

            // Assert
            using (var session = documentStore.OpenSession())
            {
                var failedMessages = session.Query<FailedMessage>().ToArray();

                Assert.AreEqual(2, failedMessages.Length, "There should be 2 failed message for subscriber 1 and subscriber 2");

                var subscriber1FailedMessage = failedMessages.FirstOrDefault(fm => fm.ProcessingAttempts.Count == 2);
                var subscriber2FailedMessage = failedMessages.FirstOrDefault(fm => fm.ProcessingAttempts.Count == 1);

                Assert.IsNotNull(subscriber1FailedMessage, "Subscriber 1 should have 2 attempts");
                Assert.IsNotNull(subscriber2FailedMessage, "Subscriber 1 should have 1 attempt");

                Assert.IsTrue(subscriber1FailedMessage.ProcessingAttempts.All(pa => pa.FailureDetails.AddressOfFailingEndpoint == PreSplitScenario.Subscriber1InputQueue), "Subscriber 1 message has mismatched failed queues");
                Assert.IsTrue(subscriber2FailedMessage.ProcessingAttempts.All(pa => pa.FailureDetails.AddressOfFailingEndpoint == PreSplitScenario.Subscriber2InputQueue), "Subscriber 2 message has mismatched failed queues");
            }
        }

        [Test]
        public void Should_combined_attempts_from_the_same_endpoint_v5_and_later()
        {
            // Arrange
            var scenarioInfo = new PreSplitScenario();

            scenarioInfo.AddV5ProcessingAttempt(PreSplitScenario.Subscriber1InputQueue, PreSplitScenario.Subscriber1Endpoint);
            scenarioInfo.AddV5ProcessingAttempt(PreSplitScenario.Subscriber1InputQueue, PreSplitScenario.Subscriber1Endpoint, true);
            scenarioInfo.AddV5ProcessingAttempt(PreSplitScenario.Subscriber2InputQueue, PreSplitScenario.Subscriber2Endpoint);

            using (var session = documentStore.OpenSession())
            {
                session.Store(scenarioInfo.FailedMessage);
                session.SaveChanges();
            }

            // Act
            var migration = CreateMigration();
            migration.Apply(documentStore);

            // Assert
            using (var session = documentStore.OpenSession())
            {
                var failedMessages = session.Query<FailedMessage>().ToArray();

                Assert.AreEqual(2, failedMessages.Length, "There should be 2 failed message for subscriber 1 and subscriber 2");

                var subscriber1FailedMessage = failedMessages.FirstOrDefault(fm => fm.ProcessingAttempts.Count == 2);
                var subscriber2FailedMessage = failedMessages.FirstOrDefault(fm => fm.ProcessingAttempts.Count == 1);

                Assert.IsNotNull(subscriber1FailedMessage, "Subscriber 1 should have 2 attempts");
                Assert.IsNotNull(subscriber2FailedMessage, "Subscriber 1 should have 1 attempt");

                Assert.IsTrue(subscriber1FailedMessage.ProcessingAttempts.All(pa => pa.FailureDetails.AddressOfFailingEndpoint == PreSplitScenario.Subscriber1InputQueue), "Subscriber 1 message has mismatched failed queues");
                Assert.IsTrue(subscriber2FailedMessage.ProcessingAttempts.All(pa => pa.FailureDetails.AddressOfFailingEndpoint == PreSplitScenario.Subscriber2InputQueue), "Subscriber 2 message has mismatched failed queues");
            }
        }

        [Test]
        public void Should_handle_larger_than_pagesize_number_of_failedmessages()
        {
            const int multiplier = 2;
            using (var session = documentStore.OpenSession())
            {
                for (var i = 0; i < SplitFailedMessageDocumentsMigration.PageSize*multiplier; i++)
                {
                    var scenarioInfo = new PreSplitScenario();
                    scenarioInfo.AddV5ProcessingAttempt(PreSplitScenario.Subscriber1InputQueue, PreSplitScenario.Subscriber1Endpoint);
                    scenarioInfo.AddV5ProcessingAttempt(PreSplitScenario.Subscriber2InputQueue, PreSplitScenario.Subscriber2Endpoint);

                    session.Store(scenarioInfo.FailedMessage);
                }

                session.SaveChanges();
            }

            // Act
            documentStore.WaitForIndexing();
            var migration = CreateMigration();
            var migrationResult = migration.Apply(documentStore);
            Console.WriteLine($"Migration Result: {migrationResult}");
            documentStore.WaitForIndexing();

            // Assert
            using (var session = documentStore.OpenSession())
            {
                RavenQueryStatistics stats;
                // ReSharper disable once ReturnValueOfPureMethodIsNotUsed
                session.Query<FailedMessage>().Customize(q => q.WaitForNonStaleResultsAsOfNow()).Statistics(out stats).Take(0).ToArray();

                var expectedCount = SplitFailedMessageDocumentsMigration.PageSize * multiplier * multiplier;

                Assert.AreEqual(expectedCount, stats.TotalResults, $"There should be {expectedCount} failed messages after split");
            }
        }

        [Test]
        public void Should_split_attempts_from_the_same_endpoint_with_v4_and_later_instance()
        {
            // Arrange
            var scenarioInfo = new PreSplitScenario();
            scenarioInfo.AddV4ProcessingAttempt(PreSplitScenario.Subscriber1InputQueue);
            scenarioInfo.AddV5ProcessingAttempt(PreSplitScenario.Subscriber1InputQueue, PreSplitScenario.Subscriber1Endpoint);
            scenarioInfo.AddV5ProcessingAttempt(PreSplitScenario.Subscriber2InputQueue, PreSplitScenario.Subscriber2Endpoint);

            using (var session = documentStore.OpenSession())
            {
                session.Store(scenarioInfo.FailedMessage);
                session.SaveChanges();
            }

            // Act
            var migration = CreateMigration();
            migration.Apply(documentStore);

            // Assert
            using (var session = documentStore.OpenSession())
            {
                var failedMessages = session.Query<FailedMessage>().ToArray();

                Assert.AreEqual(2, failedMessages.Length, "Expected FailedMessage Records is incorrect");
            }
        }

        [Test]
        public void Should_split_retryissued_failuremessages_from_two_logical_subscribers()
        {
            // Arrange
            var scenarioInfo = new PreSplitScenario(FailedMessageStatus.RetryIssued);
            scenarioInfo.AddV4ProcessingAttempt(PreSplitScenario.Subscriber1InputQueue);
            scenarioInfo.AddV4ProcessingAttempt(PreSplitScenario.Subscriber2InputQueue);
            using (var session = documentStore.OpenSession())
            {
                session.Store(scenarioInfo.FailedMessage);
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

                var firstAttemptFailure = failedMessages.SingleOrDefault(f => f.UniqueMessageId == scenarioInfo.ProcessingAttempts[0].ExpectedUniqueMessageId);
                Assert.IsNotNull(firstAttemptFailure, "Attempt for Subscriber 1 not found");
                Assert.AreEqual(firstAttemptFailure.Status, FailedMessageStatus.Unresolved, "Attempt for Subscriber 1 is not marked Unresolved");


                var secondAttemptFailure = failedMessages.SingleOrDefault(f => f.UniqueMessageId == scenarioInfo.ProcessingAttempts[1].ExpectedUniqueMessageId);
                Assert.IsNotNull(secondAttemptFailure, "Attempt for Subscriber 2 not found");
                Assert.AreEqual(secondAttemptFailure.Status, FailedMessageStatus.Unresolved, "Attempt for Subscriber 2 is not marked Unresolved");
            }
        }

        [Test]
        public void Should_split_retryissued_failuremessages_from_two_logical_subscribers_after_retrying()
        {
            // Arrange
            var scenarioInfo = new PreSplitScenario(FailedMessageStatus.RetryIssued);
            scenarioInfo.AddV5ProcessingAttempt(PreSplitScenario.Subscriber1InputQueue, PreSplitScenario.Subscriber1Endpoint);
            scenarioInfo.AddV5ProcessingAttempt(PreSplitScenario.Subscriber2InputQueue, PreSplitScenario.Subscriber2Endpoint);
            scenarioInfo.AddV5ProcessingAttempt(PreSplitScenario.Subscriber1InputQueue, PreSplitScenario.Subscriber1Endpoint, true);
            using (var session = documentStore.OpenSession())
            {
                session.Store(scenarioInfo.FailedMessage);
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

                var firstAttemptFailure = failedMessages.SingleOrDefault(f => f.UniqueMessageId == scenarioInfo.ProcessingAttempts[0].ExpectedUniqueMessageId);
                Assert.IsNotNull(firstAttemptFailure, "Attempt for Subscriber 1 not found");
                Assert.AreEqual(firstAttemptFailure.Status, FailedMessageStatus.Unresolved, "Attempt for Subscriber 1 is not marked Unresolved");


                var secondAttemptFailure = failedMessages.SingleOrDefault(f => f.UniqueMessageId == scenarioInfo.ProcessingAttempts[1].ExpectedUniqueMessageId);
                Assert.IsNotNull(secondAttemptFailure, "Attempt for Subscriber 2 not found");
                Assert.AreEqual(secondAttemptFailure.Status, FailedMessageStatus.Unresolved, "Attempt for Subscriber 2 is not marked Unresolved");
            }
        }

        [Test]
        public void Should_split_resolved_failuremessages_from_two_logical_subscribers()
        {
            // Arrange
            var scenarioInfo = new PreSplitScenario(FailedMessageStatus.Resolved);
            scenarioInfo.AddV4ProcessingAttempt(PreSplitScenario.Subscriber1InputQueue);
            scenarioInfo.AddV4ProcessingAttempt(PreSplitScenario.Subscriber2InputQueue);
            using (var session = documentStore.OpenSession())
            {
                session.Store(scenarioInfo.FailedMessage);
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

                var firstAttemptFailure = failedMessages.SingleOrDefault(f => f.UniqueMessageId == scenarioInfo.ProcessingAttempts[0].ExpectedUniqueMessageId);
                Assert.IsNotNull(firstAttemptFailure, "Attempt for Subscriber 1 not found");
                Assert.AreEqual(firstAttemptFailure.Status, FailedMessageStatus.Unresolved, "Attempt for Subscriber 1 is not marked Unresolved");


                var secondAttemptFailure = failedMessages.SingleOrDefault(f => f.UniqueMessageId == scenarioInfo.ProcessingAttempts[1].ExpectedUniqueMessageId);
                Assert.IsNotNull(secondAttemptFailure, "Attempt for Subscriber 2 not found");
                Assert.AreEqual(secondAttemptFailure.Status, FailedMessageStatus.Unresolved, "Attempt for Subscriber 2 is not marked Unresolved");
            }
        }

        [Test]
        public void Should_split_archived_failuremessages_from_two_logical_subscribers()
        {
            // Arrange
            var scenarioInfo = new PreSplitScenario(FailedMessageStatus.Archived);
            scenarioInfo.AddV4ProcessingAttempt(PreSplitScenario.Subscriber1InputQueue);
            scenarioInfo.AddV4ProcessingAttempt(PreSplitScenario.Subscriber2InputQueue);
            using (var session = documentStore.OpenSession())
            {
                session.Store(scenarioInfo.FailedMessage);
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

                var firstAttemptFailure = failedMessages.SingleOrDefault(f => f.UniqueMessageId == scenarioInfo.ProcessingAttempts[0].ExpectedUniqueMessageId);
                Assert.IsNotNull(firstAttemptFailure, "Attempt for Subscriber 1 not found");
                Assert.AreEqual(firstAttemptFailure.Status, FailedMessageStatus.Unresolved, "Attempt for Subscriber 1 is not marked Unresolved");


                var secondAttemptFailure = failedMessages.SingleOrDefault(f => f.UniqueMessageId == scenarioInfo.ProcessingAttempts[1].ExpectedUniqueMessageId);
                Assert.IsNotNull(secondAttemptFailure, "Attempt for Subscriber 2 not found");
                Assert.AreEqual(secondAttemptFailure.Status, FailedMessageStatus.Unresolved, "Attempt for Subscriber 2 is not marked Unresolved");
            }
        }

        [Test]
        public void Split_failuremessages_should_have_failure_groups()
        {
            // Arrange

            var scenarios = new List<PreSplitScenario>();

            using (var session = documentStore.OpenSession())
            {
                foreach (FailedMessageStatus status in Enum.GetValues(typeof(FailedMessageStatus)))
                {
                    var scenarioInfo = new PreSplitScenario(status);
                    scenarioInfo.AddV4ProcessingAttempt(PreSplitScenario.Subscriber1InputQueue);
                    scenarioInfo.AddV4ProcessingAttempt(PreSplitScenario.Subscriber2InputQueue);
                    scenarioInfo.AddGroup("GroupId", "Fake Group Title", "Fake Group Type");
                    scenarios.Add(scenarioInfo);

                    session.Store(scenarioInfo.FailedMessage);
                }

                session.SaveChanges();
            }
            AddClassifier(new ExceptionTypeAndStackTraceFailureClassifier());
            AddClassifier(new MessageTypeFailureClassifier());

            var attempts = scenarios.SelectMany(s => s.ProcessingAttempts.Select(pa => new { s.OriginalFailedMessageStatus, pa.ExpectedUniqueMessageId, EndpointName = pa.Attempt.Headers.ProcessingEndpointName() })).ToList();

            // Act
            var migration = CreateMigration();
            migration.Apply(documentStore);

            // Assert

            FailedMessage[] failedMessages;

            using (var session = documentStore.OpenSession())
            {
                failedMessages = session.Query<FailedMessage>().ToArray();
            }

            Assert.IsNotEmpty(failedMessages, "No Failed Messages Found");

            foreach (var failedMessage in failedMessages)
            {
                var attempt = attempts.SingleOrDefault(a => a.ExpectedUniqueMessageId == failedMessage.UniqueMessageId);

                Assert.IsNotNull(attempt, "Could not find attempt for a failed message");

                Assert.AreEqual(2, failedMessage.FailureGroups.Count, "A FailedMessage does not have all expected Failure Groups");

                var expectedPrefix = string.Format(SplitFailedMessageDocumentsMigration.GroupPrefixFormat, attempt.EndpointName);

                var nonMatchingGroups = failedMessage.FailureGroups.Where(x => x.Title.StartsWith(expectedPrefix) == false).ToArray();

                Assert.IsFalse(nonMatchingGroups.Any(), $"All groups should start with the prefix: {expectedPrefix}");
            }
        }

        class PreSplitScenario
        {
            public const string Subscriber1InputQueue = "Subscriber1@SUBSCRIBER1-MACHINE";
            public const string Subscriber1Endpoint = "Subscriber1";
            public const string Subscriber2InputQueue = "Subscriber2@SUBSCRIBER2-MACHINE";
            public const string Subscriber2Endpoint = "Subscriber2";

            public readonly string MessageId = Guid.NewGuid().ToString();
            public readonly string ReplyToAddress = "SomePublisher@PUBLISHING-MACHINE";

            public FailedMessage FailedMessage { get; }

            public FailedMessageStatus OriginalFailedMessageStatus { get; }

            public List<ProcessingAttemptInfo> ProcessingAttempts { get; } = new List<ProcessingAttemptInfo>();

            public List<FailedMessage.FailureGroup> FailureGroups { get; } = new List<FailedMessage.FailureGroup>();

            public string UniqueMessageId { get; }

            public PreSplitScenario(FailedMessageStatus originalStatus)
            {
                UniqueMessageId = DeterministicGuid.MakeId(MessageId, ReplyToAddress).ToString();
                OriginalFailedMessageStatus = originalStatus;
                FailedMessage = new FailedMessage()
                {
                    Id = FailedMessage.MakeDocumentId(UniqueMessageId),
                    UniqueMessageId = UniqueMessageId,
                    ProcessingAttempts = new List<FailedMessage.ProcessingAttempt>(),
                    Status = originalStatus,
                    FailureGroups = new List<FailedMessage.FailureGroup>()
                };
            }

            public PreSplitScenario() : this(FailedMessageStatus.Unresolved)
            { }

            public void AddV4ProcessingAttempt(string failedQ, bool isRetry = false)
            {
                var attempt = new ProcessingAttemptInfo(this, failedQ, isRetry);
                ProcessingAttempts.Add(attempt);
                FailedMessage.ProcessingAttempts.Add(attempt.Attempt);
            }

            public void AddV5ProcessingAttempt(string failedQ, string endpointName, bool isRetry = false)
            {
                var attempt = new ProcessingAttemptInfo(this, failedQ, endpointName, isRetry);
                ProcessingAttempts.Add(attempt);
                FailedMessage.ProcessingAttempts.Add(attempt.Attempt);
            }

            public void AddGroup(string id, string title, string type)
            {
                var failureGroup = new FailedMessage.FailureGroup
                {
                    Id = id,
                    Title = title,
                    Type = type
                };
                FailureGroups.Add(failureGroup);
                FailedMessage.FailureGroups.Add(failureGroup);
            }
        }

        class ProcessingAttemptInfo
        {
            public const string MessageType = "SomeMessageType";
            const string V4RetryUniqueMessageIdHeader = "ServiceControl.RetryId";
            const string V5RetryUniqueMessageIdHeader = "ServiceControl.Retry.UniqueMessageId";
            public FailedMessage.ProcessingAttempt Attempt { get; }
            public string ExpectedUniqueMessageId { get; }
            public string FailedQ { get; }
            public string EndpoingName { get; }

            public ProcessingAttemptInfo(PreSplitScenario scenario, string failedQ, bool isRetry)
            {
                FailedQ = failedQ;
                Attempt = CreateAttempt(scenario, failedQ);
                ExpectedUniqueMessageId = Attempt.Headers.UniqueId();

                if (isRetry)
                {
                    Attempt.Headers.Add(V4RetryUniqueMessageIdHeader, scenario.UniqueMessageId);
                }
            }

            public ProcessingAttemptInfo(PreSplitScenario scenario, string failedQ, string endpointName, bool isRetry)
            {
                FailedQ = failedQ;
                Attempt = CreateAttempt(scenario, failedQ);
                EndpoingName = endpointName;
                Attempt.Headers.Add(Headers.ProcessingEndpoint, endpointName);

                ExpectedUniqueMessageId = Attempt.Headers.UniqueId();
                if (isRetry)
                {
                    Attempt.Headers.Add(V5RetryUniqueMessageIdHeader, scenario.UniqueMessageId);
                }
            }

            static FailedMessage.ProcessingAttempt CreateAttempt(PreSplitScenario scenario, string failedQ)
            {
                return new FailedMessage.ProcessingAttempt
                {
                    MessageId = scenario.MessageId,
                    AttemptedAt = DateTime.UtcNow.AddDays(-1),
                    ReplyToAddress = scenario.ReplyToAddress,
                    FailureDetails = new FailureDetails
                    {
                        AddressOfFailingEndpoint = failedQ,
                        Exception = new ExceptionDetails
                        {
                            ExceptionType = "SomeExceptionType",
                            Message = "An Exception Message",
                            Source = "TestScenario"
                        }
                    },
                    Headers = new Dictionary<string, string>
                    {
                        { FaultsHeaderKeys.FailedQ, failedQ },
                        { Headers.MessageId, scenario.MessageId },
                        { Headers.ReplyToAddress, scenario.ReplyToAddress }
                    },
                    MessageMetadata = new Dictionary<string, object>
                    {
                        { "MessageType",  MessageType }
                    }
                };
            }
        }

        private SplitFailedMessageDocumentsMigration CreateMigration() => new SplitFailedMessageDocumentsMigration(failureClassifiers.ToArray());

        private EmbeddableDocumentStore documentStore;
        private IList<IFailureClassifier> failureClassifiers;

        void AddClassifier(IFailureClassifier classifier)
        {
            failureClassifiers.Add(classifier);
        }

        [SetUp]
        public void Setup()
        {
            failureClassifiers = new List<IFailureClassifier>();
            documentStore = InMemoryStoreBuilder.GetInMemoryStore();
        }

        [TearDown]
        public void Teardown()
        {
            documentStore.Dispose();
        }
    }
}