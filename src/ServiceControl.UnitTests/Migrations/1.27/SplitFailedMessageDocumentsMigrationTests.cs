namespace ServiceControl.UnitTests.Migrations._1._27
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using NServiceBus;
    using NServiceBus.Faults;
    using NServiceBus.ObjectBuilder;
    using NUnit.Framework;
    using Particular.ServiceControl.DbMigrations;
    using Raven.Client;
    using Raven.Client.Indexes;
    using ServiceControl.Contracts.Operations;
    using ServiceControl.Infrastructure;
    using ServiceControl.MessageFailures;
    using ServiceControl.MessageRedirects;
    using ServiceControl.Recoverability;
    using FailedMessage = ServiceControl.MessageFailures.FailedMessage;

    [TestFixture]
    public class When_splitting_multisubscriber_failure_attempts
    {

        [Test]
        public void Should_combine_attempts_from_the_same_endpoint_v5()
        {
            // Arrange
            var scenarioInfo = new ScenarioInfo();

            var attempts = new List<ProcessingAttemptInfo>
            {
                new ProcessingAttemptInfo(scenarioInfo.MessageId, scenarioInfo.Subscriber1InputQueue, scenarioInfo.ReplyToAddress),
                new ProcessingAttemptInfo(scenarioInfo.MessageId, scenarioInfo.Subscriber1InputQueue, scenarioInfo.ReplyToAddress),
                new ProcessingAttemptInfo(scenarioInfo.MessageId, scenarioInfo.Subscriber2InputQueue, scenarioInfo.ReplyToAddress),
            };

            //The second attempt is a retry coming from subscriber 1
            attempts[1].Attempt.Headers["ServiceControl.Retry.UniqueMessageId"] = scenarioInfo.GetOriginalUniqueId();

            FailedMessage originalFailedMessage;

            new RavenDocumentsByEntityName().Execute(documentStore);
            using (var session = documentStore.OpenSession())
            {
                originalFailedMessage = new FailedMessage
                {
                    Id = FailedMessage.MakeDocumentId(scenarioInfo.GetOriginalUniqueId()),
                    UniqueMessageId = scenarioInfo.GetOriginalUniqueId(),
                    ProcessingAttempts = attempts.Select(a => a.Attempt).ToList(),
                    Status = FailedMessageStatus.Unresolved
                };

                session.Store(originalFailedMessage);
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

                Assert.IsNotNull(subscriber1FailedMessage);
                Assert.IsNotNull(subscriber2FailedMessage);

                Assert.AreEqual(
                    originalFailedMessage.ProcessingAttempts[0].FailureDetails.AddressOfFailingEndpoint,
                    subscriber1FailedMessage.ProcessingAttempts[0].FailureDetails.AddressOfFailingEndpoint);
                Assert.AreEqual(
                    originalFailedMessage.ProcessingAttempts[1].FailureDetails.AddressOfFailingEndpoint,
                    subscriber1FailedMessage.ProcessingAttempts[1].FailureDetails.AddressOfFailingEndpoint);
            }
        }

        [Test]
        public void Should_combined_attempts_from_the_same_endpoint_v6()
        {
            // Arrange
            var scenarioInfo = new ScenarioInfo();

            var attempts = new List<ProcessingAttemptInfo>
            {
                new ProcessingAttemptInfo(scenarioInfo.MessageId, scenarioInfo.Subscriber1InputQueue, scenarioInfo.ReplyToAddress, scenarioInfo.Subscriber1Endpoint),
                new ProcessingAttemptInfo(scenarioInfo.MessageId, scenarioInfo.Subscriber1InputQueue, scenarioInfo.ReplyToAddress, scenarioInfo.Subscriber1Endpoint),
                new ProcessingAttemptInfo(scenarioInfo.MessageId, scenarioInfo.Subscriber2InputQueue, scenarioInfo.ReplyToAddress, scenarioInfo.Subscriber2Endpoint),
            };

            //The second attempt is a retry coming from V6 instance
            attempts[1].Attempt.Headers["ServiceControl.Retry.UniqueMessageId"] = scenarioInfo.GetOriginalUniqueId();

            FailedMessage originalFailedMessage;

            new RavenDocumentsByEntityName().Execute(documentStore);
            using (var session = documentStore.OpenSession())
            {
                originalFailedMessage = new FailedMessage
                {
                    Id = FailedMessage.MakeDocumentId(scenarioInfo.GetOriginalUniqueId()),
                    UniqueMessageId = scenarioInfo.GetOriginalUniqueId(),
                    ProcessingAttempts = attempts.Select(a => a.Attempt).ToList(),
                    Status = FailedMessageStatus.Unresolved
                };

                session.Store(originalFailedMessage);
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

                Assert.IsNotNull(subscriber1FailedMessage);
                Assert.IsNotNull(subscriber2FailedMessage);

                Assert.AreEqual(
                    originalFailedMessage.ProcessingAttempts[0].FailureDetails.AddressOfFailingEndpoint,
                    subscriber1FailedMessage.ProcessingAttempts[0].FailureDetails.AddressOfFailingEndpoint);
                Assert.AreEqual(
                    originalFailedMessage.ProcessingAttempts[1].FailureDetails.AddressOfFailingEndpoint,
                    subscriber1FailedMessage.ProcessingAttempts[1].FailureDetails.AddressOfFailingEndpoint);
            }
        }

        [Test]
        public void Should_combine_attempts_from_the_same_endpoint_with_v5_and_v6_instance()
        {
            // Arrange
            var scenarioInfo = new ScenarioInfo();

            var attempts = new List<ProcessingAttemptInfo>
            {
                new ProcessingAttemptInfo(scenarioInfo.MessageId, scenarioInfo.Subscriber1InputQueue, scenarioInfo.ReplyToAddress),
                new ProcessingAttemptInfo(scenarioInfo.MessageId, scenarioInfo.Subscriber1InputQueue, scenarioInfo.ReplyToAddress, scenarioInfo.Subscriber1Endpoint),
                new ProcessingAttemptInfo(scenarioInfo.MessageId, scenarioInfo.Subscriber2InputQueue, scenarioInfo.ReplyToAddress, scenarioInfo.Subscriber2Endpoint),
            };

            //The second attempt is a retry coming from V6 instance
            attempts[1].Attempt.Headers["ServiceControl.Retry.UniqueMessageId"] = scenarioInfo.GetOriginalUniqueId();

            FailedMessage originalFailedMessage;

            new RavenDocumentsByEntityName().Execute(documentStore);
            using (var session = documentStore.OpenSession())
            {
                originalFailedMessage = new FailedMessage
                {
                    Id = FailedMessage.MakeDocumentId(scenarioInfo.GetOriginalUniqueId()),
                    UniqueMessageId = scenarioInfo.GetOriginalUniqueId(),
                    ProcessingAttempts = attempts.Select(a => a.Attempt).ToList(),
                    Status = FailedMessageStatus.Unresolved
                };

                session.Store(originalFailedMessage);
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

                Assert.IsNotNull(subscriber1FailedMessage);
                Assert.IsNotNull(subscriber2FailedMessage);

                Assert.AreEqual(
                    originalFailedMessage.ProcessingAttempts[0].FailureDetails.AddressOfFailingEndpoint,
                    subscriber1FailedMessage.ProcessingAttempts[0].FailureDetails.AddressOfFailingEndpoint);
                Assert.AreEqual(
                    originalFailedMessage.ProcessingAttempts[1].FailureDetails.AddressOfFailingEndpoint,
                    subscriber1FailedMessage.ProcessingAttempts[1].FailureDetails.AddressOfFailingEndpoint);
            }
        }

        [Test]
        public void Should_combined_redirected_endpoints()
        {
            // Arrange
            var scenarioInfo = new ScenarioInfo();

            var attempts = new List<ProcessingAttemptInfo>
            {
                new ProcessingAttemptInfo(scenarioInfo.MessageId, scenarioInfo.Subscriber1InputQueue, scenarioInfo.ReplyToAddress, scenarioInfo.Subscriber1Endpoint),
                new ProcessingAttemptInfo(scenarioInfo.MessageId, scenarioInfo.Subscriber2InputQueue, scenarioInfo.ReplyToAddress, scenarioInfo.Subscriber1Endpoint),
            };

            //The second attempt is a retry coming from V6 instance
            attempts[1].Attempt.Headers["ServiceControl.Retry.UniqueMessageId"] = scenarioInfo.GetOriginalUniqueId();


            new RavenDocumentsByEntityName().Execute(documentStore);
            using (var session = documentStore.OpenSession())
            {
                var redirects = MessageRedirectsCollection.GetOrCreate(session);

                redirects.Redirects.Add(new MessageRedirect
                {
                    FromPhysicalAddress = scenarioInfo.Subscriber1InputQueue,
                    ToPhysicalAddress = scenarioInfo.Subscriber2InputQueue,
                    LastModifiedTicks = DateTime.UtcNow.Ticks
                });

                redirects.Save(session);

                var failedMessage = new FailedMessage
                {
                    Id = FailedMessage.MakeDocumentId(scenarioInfo.GetOriginalUniqueId()),
                    UniqueMessageId = scenarioInfo.GetOriginalUniqueId(),
                    ProcessingAttempts = attempts.Select(a => a.Attempt).ToList(),
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

                Assert.AreEqual(1, failedMessages.Length, "There should be 1 failed message");

                var failedMessage = failedMessages.Single();
                Assert.AreEqual(failedMessage.ProcessingAttempts.Count, 2, $"Attempt for {attempts[0].Attempt.FailureDetails.AddressOfFailingEndpoint} is not marked Unresolved");

                Assert.AreEqual(failedMessage.UniqueMessageId, attempts[0].UniqueMessageId);
            }
        }

        [Test]
        public void Should_split_unresolved_failuremessages_from_two_logical_subscribers()
        {
            // Arrange
            var scenarioInfo = new ScenarioInfo();

            var attempts = new List<ProcessingAttemptInfo>
            {
                new ProcessingAttemptInfo(scenarioInfo.MessageId, scenarioInfo.Subscriber1InputQueue, scenarioInfo.ReplyToAddress),
                new ProcessingAttemptInfo(scenarioInfo.MessageId, scenarioInfo.Subscriber2InputQueue, scenarioInfo.ReplyToAddress),
            };


            new RavenDocumentsByEntityName().Execute(documentStore);
            using (var session = documentStore.OpenSession())
            {
                var failedMessage = new FailedMessage
                {
                    Id = FailedMessage.MakeDocumentId(scenarioInfo.GetOriginalUniqueId()),
                    UniqueMessageId = scenarioInfo.GetOriginalUniqueId(),
                    ProcessingAttempts = attempts.Select(a => a.Attempt).ToList(),
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

                var firstAttemptFailure = failedMessages.SingleOrDefault(f => f.UniqueMessageId == attempts[0].UniqueMessageId);
                Assert.IsNotNull(firstAttemptFailure, $"Attempt for {attempts[0].Attempt.FailureDetails.AddressOfFailingEndpoint} not found");
                Assert.AreEqual(firstAttemptFailure.Status, FailedMessageStatus.Unresolved, $"Attempt for {attempts[0].Attempt.FailureDetails.AddressOfFailingEndpoint} is not marked Unresolved");


                var secondAttemptFailure = failedMessages.SingleOrDefault(f => f.UniqueMessageId == attempts[1].UniqueMessageId);
                Assert.IsNotNull(secondAttemptFailure, $"Attempt for {attempts[1].Attempt.FailureDetails.AddressOfFailingEndpoint} not found");
                Assert.AreEqual(secondAttemptFailure.Status, FailedMessageStatus.Unresolved, $"Attempt for {attempts[1].Attempt.FailureDetails.AddressOfFailingEndpoint} is not marked Unresolved");
            }
        }

        [Test]
        public void Should_split_failuremessages_from_two_logical_subscribers_with_redirects()
        {
            // Arrange
            var scenarioInfo = new ScenarioInfo();

            var redirectForSubscriber2 = scenarioInfo.Subscriber2InputQueue + "2";

            var attempts = new List<ProcessingAttemptInfo>
            {
                new ProcessingAttemptInfo(scenarioInfo.MessageId, scenarioInfo.Subscriber1InputQueue, scenarioInfo.ReplyToAddress, scenarioInfo.Subscriber1Endpoint),
                new ProcessingAttemptInfo(scenarioInfo.MessageId, scenarioInfo.Subscriber2InputQueue, scenarioInfo.ReplyToAddress, scenarioInfo.Subscriber2Endpoint),
                new ProcessingAttemptInfo(scenarioInfo.MessageId, redirectForSubscriber2, scenarioInfo.ReplyToAddress, scenarioInfo.Subscriber2Endpoint),
            };

            //The third attempt is a redirected retry
            attempts[2].Attempt.Headers["ServiceControl.Retry.UniqueMessageId"] = scenarioInfo.GetOriginalUniqueId();

            new RavenDocumentsByEntityName().Execute(documentStore);
            using (var session = documentStore.OpenSession())
            {
                var redirects = MessageRedirectsCollection.GetOrCreate(session);

                redirects.Redirects.Add(new MessageRedirect
                {
                    FromPhysicalAddress = scenarioInfo.Subscriber2InputQueue,
                    ToPhysicalAddress = redirectForSubscriber2,
                    LastModifiedTicks = DateTime.UtcNow.Ticks
                });

                redirects.Save(session);

                var failedMessage = new FailedMessage
                {
                    Id = FailedMessage.MakeDocumentId(scenarioInfo.GetOriginalUniqueId()),
                    UniqueMessageId = scenarioInfo.GetOriginalUniqueId(),
                    ProcessingAttempts = attempts.Select(a => a.Attempt).ToList(),
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

                var firstAttemptFailure = failedMessages.SingleOrDefault(f => f.UniqueMessageId == attempts[0].UniqueMessageId);
                Assert.IsNotNull(firstAttemptFailure, $"Attempt for {attempts[0].Attempt.FailureDetails.AddressOfFailingEndpoint} not found");
                Assert.AreEqual(firstAttemptFailure.Status, FailedMessageStatus.Unresolved, $"Attempt for {attempts[0].Attempt.FailureDetails.AddressOfFailingEndpoint} is not marked Unresolved");


                var secondAttemptFailure = failedMessages.SingleOrDefault(f => f.UniqueMessageId == attempts[1].UniqueMessageId);
                Assert.IsNotNull(secondAttemptFailure, $"Attempts for redirected {attempts[1].Attempt.FailureDetails.AddressOfFailingEndpoint} not found");
                Assert.AreEqual(secondAttemptFailure.ProcessingAttempts.Count, 2, "ProcessingAttempts Count");
            }
        }

        [Test]
        public void Should_split_retryissued_failuremessages_from_two_logical_subscribers()
        {
            // Arrange
            var scenarioInfo = new ScenarioInfo();

            var attempts = new List<ProcessingAttemptInfo>
            {
                new ProcessingAttemptInfo(scenarioInfo.MessageId, scenarioInfo.Subscriber1InputQueue, scenarioInfo.ReplyToAddress),
                new ProcessingAttemptInfo(scenarioInfo.MessageId, scenarioInfo.Subscriber2InputQueue, scenarioInfo.ReplyToAddress),
            };


            new RavenDocumentsByEntityName().Execute(documentStore);
            using (var session = documentStore.OpenSession())
            {
                var failedMessage = new FailedMessage
                {
                    Id = FailedMessage.MakeDocumentId(scenarioInfo.GetOriginalUniqueId()),
                    UniqueMessageId = scenarioInfo.GetOriginalUniqueId(),
                    ProcessingAttempts = attempts.Select(a => a.Attempt).ToList(),
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

                var firstAttemptFailure = failedMessages.SingleOrDefault(f => f.UniqueMessageId == attempts[0].UniqueMessageId);
                Assert.IsNotNull(firstAttemptFailure, $"Attempt for {attempts[0].Attempt.FailureDetails.AddressOfFailingEndpoint} not found");
                Assert.AreEqual(firstAttemptFailure.Status, FailedMessageStatus.Unresolved, $"Attempt for {attempts[0].Attempt.FailureDetails.AddressOfFailingEndpoint} is not marked Unresolved");


                var secondAttemptFailure = failedMessages.SingleOrDefault(f => f.UniqueMessageId == attempts[1].UniqueMessageId);
                Assert.IsNotNull(secondAttemptFailure, $"Attempt for {attempts[1].Attempt.FailureDetails.AddressOfFailingEndpoint} not found");
                Assert.AreEqual(secondAttemptFailure.Status, FailedMessageStatus.RetryIssued, $"Attempt for {attempts[1].Attempt.FailureDetails.AddressOfFailingEndpoint} is not marked RetryIssued");
            }
        }

        [Test]
        public void Should_split_resolved_failuremessages_from_two_logical_subscribers()
        {
            // Arrange
            var scenarioInfo = new ScenarioInfo();

            var attempts = new List<ProcessingAttemptInfo>
            {
                new ProcessingAttemptInfo(scenarioInfo.MessageId, scenarioInfo.Subscriber1InputQueue, scenarioInfo.ReplyToAddress),
                new ProcessingAttemptInfo(scenarioInfo.MessageId, scenarioInfo.Subscriber2InputQueue, scenarioInfo.ReplyToAddress),
            };


            new RavenDocumentsByEntityName().Execute(documentStore);
            using (var session = documentStore.OpenSession())
            {
                var failedMessage = new FailedMessage
                {
                    Id = FailedMessage.MakeDocumentId(scenarioInfo.GetOriginalUniqueId()),
                    UniqueMessageId = scenarioInfo.GetOriginalUniqueId(),
                    ProcessingAttempts = attempts.Select(a => a.Attempt).ToList(),
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

                var firstAttemptFailure = failedMessages.SingleOrDefault(f => f.UniqueMessageId == attempts[0].UniqueMessageId);
                Assert.IsNotNull(firstAttemptFailure, $"Attempt for {attempts[0].Attempt.FailureDetails.AddressOfFailingEndpoint} not found");
                Assert.AreEqual(firstAttemptFailure.Status, FailedMessageStatus.Unresolved, $"Attempt for {attempts[0].Attempt.FailureDetails.AddressOfFailingEndpoint} is not marked Unresolved");

                //HINT: In both cases the status should be Unresolved as we do not know which attempt retry caused the whole document to be marked as resolved
                var secondAttemptFailure = failedMessages.SingleOrDefault(f => f.UniqueMessageId == attempts[1].UniqueMessageId);
                Assert.IsNotNull(secondAttemptFailure, $"Attempt for {attempts[1].Attempt.FailureDetails.AddressOfFailingEndpoint} not found");
                Assert.AreEqual(secondAttemptFailure.Status, FailedMessageStatus.Unresolved, $"Attempt for {attempts[1].Attempt.FailureDetails.AddressOfFailingEndpoint} is not marked Resolved");
            }
        }

        [Test]
        public void Should_split_archived_failuremessages_from_two_logical_subscribers()
        {
            // Arrange
            var scenarioInfo = new ScenarioInfo();

            var attempts = new List<ProcessingAttemptInfo>
            {
                new ProcessingAttemptInfo(scenarioInfo.MessageId, scenarioInfo.Subscriber1InputQueue, scenarioInfo.ReplyToAddress),
                new ProcessingAttemptInfo(scenarioInfo.MessageId, scenarioInfo.Subscriber2InputQueue, scenarioInfo.ReplyToAddress),
            };


            new RavenDocumentsByEntityName().Execute(documentStore);
            using (var session = documentStore.OpenSession())
            {
                var failedMessage = new FailedMessage
                {
                    Id = FailedMessage.MakeDocumentId(scenarioInfo.GetOriginalUniqueId()),
                    UniqueMessageId = scenarioInfo.GetOriginalUniqueId(),
                    ProcessingAttempts = attempts.Select(a => a.Attempt).ToList(),
                    Status = FailedMessageStatus.Archived
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

                var firstAttemptFailure = failedMessages.SingleOrDefault(f => f.UniqueMessageId == attempts[0].UniqueMessageId);
                Assert.IsNotNull(firstAttemptFailure, $"Attempt for {attempts[0].Attempt.FailureDetails.AddressOfFailingEndpoint} not found");
                Assert.AreEqual(firstAttemptFailure.Status, FailedMessageStatus.Unresolved, $"Attempt for {attempts[0].Attempt.FailureDetails.AddressOfFailingEndpoint} is not marked Unresolved");


                var secondAttemptFailure = failedMessages.SingleOrDefault(f => f.UniqueMessageId == attempts[1].UniqueMessageId);
                Assert.IsNotNull(secondAttemptFailure, $"Attempt for {attempts[1].Attempt.FailureDetails.AddressOfFailingEndpoint} not found");
                Assert.AreEqual(secondAttemptFailure.Status, FailedMessageStatus.Archived, $"Attempt for {attempts[1].Attempt.FailureDetails.AddressOfFailingEndpoint} is not marked Archived");
            }
        }

        [Test]
        public void Split_failuremessages_should_have_failure_groups()
        {
            // Arrange
            var scenarioInfo = new ScenarioInfo();

            var attempts = new List<ProcessingAttemptInfo>
            {
                new ProcessingAttemptInfo(scenarioInfo.MessageId, scenarioInfo.Subscriber1InputQueue, scenarioInfo.ReplyToAddress),
                new ProcessingAttemptInfo(scenarioInfo.MessageId, scenarioInfo.Subscriber2InputQueue, scenarioInfo.ReplyToAddress),
            };


            new RavenDocumentsByEntityName().Execute(documentStore);
            using (var session = documentStore.OpenSession())
            {
                var failedMessage = new FailedMessage
                {
                    Id = FailedMessage.MakeDocumentId(scenarioInfo.GetOriginalUniqueId()),
                    UniqueMessageId = scenarioInfo.GetOriginalUniqueId(),
                    ProcessingAttempts = attempts.Select(a => a.Attempt).ToList(),
                    Status = FailedMessageStatus.Unresolved
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

                Assert.IsTrue(failedMessages.All(f => f.FailureGroups.Count == 1), "Messages are missing failure groups");

                var group = failedMessages[0].FailureGroups.First();

                Assert.IsNotNull(group, "Failure Group should not be null");
                Assert.AreEqual("GroupId", group.Id);
            }
        }

        private string OldUniqueMessageId(string messageId, string processingEndpoint = null, string replyToAddress = null, string failedQ = null)
            => DeterministicGuid.MakeId(messageId, processingEndpoint ?? replyToAddress ?? failedQ).ToString();

        class ScenarioInfo
        {
            public string MessageId = Guid.NewGuid().ToString();
            public string Subscriber1InputQueue = "Subscriber1@SUBSCRIBER1-MACHINE";
            public string Subscriber1Endpoint = "Subscriber1";
            public string Subscriber2InputQueue = "Subscriber2@SUBSCRIBER2-MACHINE";
            public string Subscriber2Endpoint = "Subscriber2";
            public string ReplyToAddress = "SomePublisher@PUBLISHING-MACHINE";

            public string GetOriginalUniqueId()
            {
                return DeterministicGuid.MakeId(MessageId, ReplyToAddress).ToString();
            }
        }

        class ProcessingAttemptInfo
        {
            public FailedMessage.ProcessingAttempt Attempt { get; set; }
            public string UniqueMessageId { get; set; }

            public ProcessingAttemptInfo(string messageId, string failedQ, string replyToAddress)
            {
                Attempt = new FailedMessage.ProcessingAttempt
                {
                    MessageId = messageId,
                    AttemptedAt = DateTime.UtcNow.AddDays(-1),
                    ReplyToAddress = replyToAddress,
                    FailureDetails = new FailureDetails
                    {
                        AddressOfFailingEndpoint = failedQ
                    },
                    Headers = new Dictionary<string, string>
                    {
                        { FaultsHeaderKeys.FailedQ, failedQ }
                    },
                    MessageMetadata = new Dictionary<string, object>
                    {
                        { "MessageType", "SomeMessageType" }
                    }
                };
                UniqueMessageId = new Dictionary<string, string>
                {
                    { FaultsHeaderKeys.FailedQ, failedQ }
                }.UniqueId();
            }

            public ProcessingAttemptInfo(string messageId, string failedQ, string replyToAddress, string endpointName) : this(messageId, failedQ, replyToAddress)
            {
                Attempt.Headers.Add(Headers.ProcessingEndpoint,endpointName);
            }
        }

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
                ["NServiceBus.FailedQ"] = failedQ
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
                AddressOfFailingEndpoint = failedQ,
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