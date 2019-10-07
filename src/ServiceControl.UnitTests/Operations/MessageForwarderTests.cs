namespace ServiceControl.UnitTests.Operations
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Extensibility;
    using NServiceBus.Transport;
    using NUnit.Framework;
    using ServiceControl.Operations;

    [TestFixture]
    public class MessageForwarderTests
    {
        [SetUp]
        public void SetUp()
        {
            dispatcher = new FakeDispatcher();
            forwarder = new MessageForwarder(dispatcher);
        }

        [Test]
        public async Task Messages_should_last_as_long_as_possible()
        {
            var headers = new Dictionary<string, string>
            {
                {Headers.TimeToBeReceived, TimeSpan.FromMinutes(12).ToString()}
            };

            var context = new MessageContext("messageId", headers, Array.Empty<byte>(), new TransportTransaction(), new CancellationTokenSource(), new ContextBag());
            await forwarder.Forward(context, "forwardingAddress");

            Assert.IsFalse(dispatcher.TransportOperations.UnicastTransportOperations[0].Message.Headers.ContainsKey(Headers.TimeToBeReceived), "TimeToBeReceived header should be removed if present");
        }

        FakeDispatcher dispatcher;
        MessageForwarder forwarder;

        class FakeDispatcher : IDispatchMessages
        {
            public TransportOperations TransportOperations { get; private set; }

            public Task Dispatch(TransportOperations outgoingMessages, TransportTransaction transaction, ContextBag context)
            {
                TransportOperations = outgoingMessages;
                return Task.CompletedTask;
            }
        }
    }
}