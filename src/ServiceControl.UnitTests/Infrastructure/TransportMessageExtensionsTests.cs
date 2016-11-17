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
            var message = new TransportMessage(Guid.Empty.ToString(),new Dictionary<string, string>());
            var exception = Assert.Throws<Exception>(() => { message.ProcessingEndpointName(); });
            Assert.AreEqual("No processing endpoint could be determined for message (00000000-0000-0000-0000-000000000000)",exception.Message);
        }

        [Test]
        public void No_headers_with_message_type_should_throw_with_message_id_and_types()
        {
            var message = new TransportMessage(Guid.Empty.ToString(), new Dictionary<string, string>
                                                                      {
                                                                          { Headers.EnclosedMessageTypes ,"TheMessageType"}
                                                                      });
            var exception = Assert.Throws<Exception>(() => { message.ProcessingEndpointName(); });
            Assert.AreEqual("No processing endpoint could be determined for message (00000000-0000-0000-0000-000000000000) with EnclosedMessageTypes (TheMessageType)", exception.Message);
        }

        [Test]
        public void With_ProcessingEndpoint_header_should_return_processing_endpoint()
        {
            var message = new TransportMessage(Guid.Empty.ToString(), new Dictionary<string, string>
                                                                      {
                                                                          { Headers.ProcessingEndpoint ,"TheEndpoint"}
                                                                      });
            Assert.AreEqual("TheEndpoint",message.ProcessingEndpointName());
        }

        [Test]
        public void With_FailedQ_header_should_return_FailedQ()
        {
            var message = new TransportMessage(Guid.Empty.ToString(), new Dictionary<string, string>
                                                                      {
                                                                          { "NServiceBus.FailedQ" ,"TheEndpoint"}
                                                                      });
            Assert.AreEqual("TheEndpoint",message.ProcessingEndpointName());
        }

        [Test]
        public void With_ReplyToAddress_should_return_ReplyToAddress()
        {
            var headers = new Dictionary<string, string>
            {
                [Headers.ReplyToAddress] = new Address("TheEndpoint", String.Empty).ToString()
            };
            var message = new TransportMessage(null, headers);
            Assert.AreEqual("TheEndpoint", message.ProcessingEndpointName());
        }
    }
}
