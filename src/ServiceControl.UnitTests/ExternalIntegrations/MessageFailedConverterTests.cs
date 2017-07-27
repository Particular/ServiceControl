namespace ServiceControl.UnitTests.ExternalIntegrations
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using NUnit.Framework;
    using ServiceControl.Contracts;
    using ServiceControl.ExternalIntegrations;
    using ServiceControl.MessageFailures;
    using ExceptionDetails = ServiceControl.Contracts.Operations.ExceptionDetails;
    using FailureDetails = ServiceControl.Contracts.Operations.FailureDetails;

    [TestFixture]
    public class MessageFailedConverterTests
    {
        [Test]
        public void Archive_status_maps_to_archive()
        {
            var failedMessage = new FailedMessageBuilder(FailedMessageStatus.Archived)
                .AddProcessingAttempt(pa => { })
                .Build();

            var result = failedMessage.ToEvent();

            Assert.AreEqual(MessageFailed.MessageStatus.ArchivedFailure, result.Status);
        }

        [Test]
        public void Unresolved_failure_is_not_considered_repeated_if_it_has_only_one_processing_attempt()
        {
            var failedMessage = new FailedMessageBuilder(FailedMessageStatus.Unresolved)
                .AddProcessingAttempt(pa => { })
                .Build();

            var result = failedMessage.ToEvent();
            Assert.AreEqual(MessageFailed.MessageStatus.Failed, result.Status);
        }

        [Test]
        public void Unresolved_failure_is_considered_repeated_if_it_has_more_than_one_processing_attempt()
        {
            var failedMessage = new FailedMessageBuilder(FailedMessageStatus.Unresolved)
                .AddProcessingAttempt(pa => { })
                .AddProcessingAttempt(pa => { })
                .Build();

            var result = failedMessage.ToEvent();
            Assert.AreEqual(MessageFailed.MessageStatus.RepeatedFailure, result.Status);
        }

        [Test]
        public void If_not_present_in_metadata_body_is_ignored()
        {
            var failedMessage = new FailedMessageBuilder(FailedMessageStatus.Unresolved)
                .AddProcessingAttempt(pa => { })
                .Build();

            var result = failedMessage.ToEvent();
            Assert.IsNull(result.MessageDetails.Body);
        }

        [Test]
        public void If_message_type_is_missing_it_is_ignored()
        {
            var failedMessage = new FailedMessageBuilder(FailedMessageStatus.Unresolved)
                .SetMessageType(null)
                .AddProcessingAttempt(pa => { })
                .Build();

            var result = failedMessage.ToEvent();
            Assert.IsNull(result.MessageType);
        }

        [Test]
        public void If_content_type_is_missing_it_is_ignored()
        {
            var failedMessage = new FailedMessageBuilder(FailedMessageStatus.Unresolved)
                .SetContentType(null)
                .AddProcessingAttempt(pa => { })
                .Build();

            var result = failedMessage.ToEvent();
            Assert.IsNull(result.MessageDetails.ContentType);
        }

        [Test]
        public void Body_is_mapped_from_metadata_of_last_processing_attempt()
        {
            var failedMessage = new FailedMessageBuilder(FailedMessageStatus.Unresolved)
                .AddProcessingAttempt(pa => { pa.MessageMetadata["Body"] = "Beautiful Body"; })
                .Build();

            var result = failedMessage.ToEvent();
            Assert.AreEqual("Beautiful Body", result.MessageDetails.Body);
        }

        private class FailedMessageBuilder
        {
            private readonly FailedMessageStatus messageStatus;
            string messageType = "SomeMessage";
            string contentType = "application/json";
            private List<Action<FailedMessage.ProcessingAttempt>> processingAttempts = new List<Action<FailedMessage.ProcessingAttempt>>();

            public FailedMessageBuilder(FailedMessageStatus messageStatus)
            {
                this.messageStatus = messageStatus;
            }

            public FailedMessageBuilder SetMessageType(string messageType)
            {
                this.messageType = messageType;
                return this;
            }

            public FailedMessageBuilder SetContentType(string contentType)
            {
                this.contentType = contentType;
                return this;
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
                        var messageMetadata = new Dictionary<string, object>
                        {
                            {"SendingEndpoint",new Contracts.Operations.EndpointDetails()},
                            {"ReceivingEndpoint",new Contracts.Operations.EndpointDetails()},
                        };
                        if (messageType != null)
                        {
                            messageMetadata["MessageType"] = messageType;
                        }
                        if (contentType != null)
                        {
                            messageMetadata["ContentType"] = contentType;
                        }
                        var attempt = new FailedMessage.ProcessingAttempt
                        {
                            FailureDetails = new FailureDetails
                            {
                                Exception = new ExceptionDetails()
                            },
                            MessageMetadata = messageMetadata,
                        };
                        x(attempt);
                        return attempt;
                    }).ToList(),
                    Status = messageStatus
                };
            }
        }
    }
}