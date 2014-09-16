namespace ServiceControl.UnitTests.CompositeViews
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using NUnit.Framework;
    using Raven.Client;
    using ServiceControl.Contracts.Failures;
    using ServiceControl.ExternalIntegrations;
    using ServiceControl.MessageFailures;
    using ServiceControl.UnitTests.Infrastructure.RavenDB;
    using ExceptionDetails = ServiceControl.Contracts.Operations.ExceptionDetails;
    using FailureDetails = ServiceControl.Contracts.Operations.FailureDetails;

    [TestFixture]
    public class ExternalIntegrationsFailedMessagesIndexTests : TestWithRavenDB
    {
        [Test]
        public void Archive_status_maps_to_archive()
        {
            using (var session = documentStore.OpenSession())
            {
                var failedMessage = new FailedMessageBuilder(FailedMessageStatus.Archived)
                    .AddProcessingAttempt(pa => { })
                    .Build();
                session.Store(failedMessage);
                session.SaveChanges();
            }

            var result = WaitForIndexingAndReadFirst();
            Assert.AreEqual(MessageStatus.ArchivedFailure, result.Status);
        }

        [Test]
        public void Unresolved_failure_is_not_considered_repeated_if_it_has_only_one_processing_attempt()
        {
            using (var session = documentStore.OpenSession())
            {
                var failedMessage = new FailedMessageBuilder(FailedMessageStatus.Unresolved)
                    .AddProcessingAttempt(pa => { })
                    .Build();
                session.Store(failedMessage);
                session.SaveChanges();
            }

            var result = WaitForIndexingAndReadFirst();
            Assert.AreEqual(MessageStatus.Failed, result.Status);
        }

        [Test]
        public void Unresolved_failure_is_considered_repeated_if_it_has_more_than_one_processing_attempt()
        {
            using (var session = documentStore.OpenSession())
            {
                var failedMessage = new FailedMessageBuilder(FailedMessageStatus.Unresolved)
                    .AddProcessingAttempt(pa => { })
                    .AddProcessingAttempt(pa => { })
                    .Build();

                session.Store(failedMessage);
                session.SaveChanges();
            }

            var result = WaitForIndexingAndReadFirst();
            Assert.AreEqual(MessageStatus.RepeatedFailure, result.Status);
        }

        [Test]
        public void If_not_present_in_metadata_body_is_ignored()
        {
            using (var session = documentStore.OpenSession())
            {
                var failedMessage = new FailedMessageBuilder(FailedMessageStatus.Unresolved)
                    .AddProcessingAttempt(pa => { })
                    .Build();

                session.Store(failedMessage);
                session.SaveChanges();
            }

            var result = WaitForIndexingAndReadFirst();
            Assert.IsNull(result.MessageDetails.Body);
        }

        [Test]
        public void Body_is_mapped_from_metadata_of_last_processing_attempt()
        {
            using (var session = documentStore.OpenSession())
            {
                var failedMessage = new FailedMessageBuilder(FailedMessageStatus.Unresolved)
                    .AddProcessingAttempt(pa => { pa.MessageMetadata["Body"] = "Beautiful Body"; })
                    .Build();
                session.Store(failedMessage);
                session.SaveChanges();
            }

            var result = WaitForIndexingAndReadFirst();
            Assert.AreEqual("Beautiful Body", result.MessageDetails.Body);
        }

        MessageFailed WaitForIndexingAndReadFirst()
        {
            WaitForIndexing(documentStore);
            MessageFailed result;
            using (var session = documentStore.OpenSession())
            {
                result = session.Query<MessageFailed, ExternalIntegrationsFailedMessagesIndex>()
                    .Customize(c => c.WaitForNonStaleResults())
                    .ProjectFromIndexFieldsInto<MessageFailed>()
                    .Single();
            }
            return result;
        }

        private class FailedMessageBuilder
        {
            private readonly FailedMessageStatus messageStatus;
            private List<Action<FailedMessage.ProcessingAttempt>> processingAttempts = new List<Action<FailedMessage.ProcessingAttempt>>();

            public FailedMessageBuilder(FailedMessageStatus messageStatus)
            {
                this.messageStatus = messageStatus;
            }

            public FailedMessageBuilder AddProcessingAttempt(Action<FailedMessage.ProcessingAttempt> callback)
            {
                processingAttempts.Add(callback);
                return this;
            }

            public FailedMessage Build()
            {
                return new FailedMessage
                {
                    ProcessingAttempts = processingAttempts.Select(x =>
                    {
                        var attempt = new FailedMessage.ProcessingAttempt
                        {
                            FailureDetails = new FailureDetails
                            {
                                Exception = new ExceptionDetails()
                            },
                            MessageMetadata = new Dictionary<string, object>()
                            {
                                {"SendingEndpoint",new Contracts.Operations.EndpointDetails()},
                                {"ReceivingEndpoint",new Contracts.Operations.EndpointDetails()}
                            }
                        };
                        x(attempt);
                        return attempt;
                    }).ToList(),
                    Status = messageStatus
                };
            }
        }

        [SetUp]
        public void SetUp()
        {
            documentStore = InMemoryStoreBuilder.GetInMemoryStore();

            var customIndex = new ExternalIntegrationsFailedMessagesIndex();
            customIndex.Execute(documentStore);
        }

        [TearDown]
        public void TearDown()
        {
            documentStore.Dispose();
        }

        IDocumentStore documentStore;
    }
}