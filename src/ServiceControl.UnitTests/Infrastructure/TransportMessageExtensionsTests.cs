namespace ServiceControl.UnitTests.Infrastructure
{
    using System;
    using System.Collections.Generic;
    using NServiceBus;
    using NUnit.Framework;

    [TestFixture]
    public class TransportMessageExtensionsTests
    {
        [Test]
        public void No_headers_should_throw_with_message_id()
        {
            var headers = new Dictionary<string, string>
            {
                {Headers.MessageId, Guid.Empty.ToString() }
            };
            var exception = Assert.Throws<Exception>(() => { headers.ProcessingEndpointName(); });
            Assert.AreEqual("No processing endpoint could be determined for message (00000000-0000-0000-0000-000000000000)", exception.Message);
        }

        [Test]
        public void No_headers_with_message_type_should_throw_with_message_id_and_types()
        {
            var headers = new Dictionary<string, string>
            {
                {Headers.MessageId, Guid.Empty.ToString() },
                { Headers.EnclosedMessageTypes ,"TheMessageType"}
            };
            var exception = Assert.Throws<Exception>(() => { headers.ProcessingEndpointName(); });
            Assert.AreEqual("No processing endpoint could be determined for message (00000000-0000-0000-0000-000000000000) with EnclosedMessageTypes (TheMessageType)", exception.Message);
        }

        [Test]
        public void With_ProcessingEndpoint_header_should_return_processing_endpoint()
        {
            var headers = new Dictionary<string, string>
            {
                { Headers.ProcessingEndpoint ,"TheEndpoint"}
            };
            Assert.AreEqual("TheEndpoint", headers.ProcessingEndpointName());
        }

        [Test]
        public void With_FailedQ_header_should_return_FailedQ()
        {
            var headers = new Dictionary<string, string>
            {
                { "NServiceBus.FailedQ" ,"TheEndpoint"}
            };
            Assert.AreEqual("TheEndpoint", headers.ProcessingEndpointName());
        }

        [Test]
        public void With_ReplyToAddress_should_return_ReplyToAddress()
        {
            var headers = new Dictionary<string, string>
            {
                [Headers.ReplyToAddress] = "TheEndpoint"
            };
            Assert.AreEqual("TheEndpoint", headers.ProcessingEndpointName());
        }
    }
}
