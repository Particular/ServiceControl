namespace ServiceControl.UnitTests.Operations
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using NServiceBus;
    using NServiceBus.Transports;
    using NServiceBus.Unicast;
    using NUnit.Framework;
    using ServiceControl.Operations.BodyStorage;
    using ServiceControl.Recoverability;

    [TestFixture]
    public class ReturnToSenderDequeuerTests
    {
        [Test]
        public void It_removes_staging_id_header()
        {
            var sender = new FakeSender();

            var headers = new Dictionary<string, string>
            {
                ["ServiceControl.Retry.StagingId"] = "SomeId",
                ["ServiceControl.TargetEndpointAddress"] = "TargetEndpoint"
            };
            var message = new TransportMessage(Guid.NewGuid().ToString(), headers);

            ReturnToSenderDequeuer.HandleMessage(message, new FakeBodyStorage(), sender);

            Assert.IsFalse(sender.Message.Headers.ContainsKey("ServiceControl.Retry.StagingId"));
        }

        [Test]
        public void It_fetches_the_body_if_provided()
        {
            var sender = new FakeSender();

            var headers = new Dictionary<string, string>
            {
                ["ServiceControl.Retry.StagingId"] = "SomeId",
                ["ServiceControl.TargetEndpointAddress"] = "TargetEndpoint",
                ["ServiceControl.Retry.Attempt.MessageId"] = "MessageBodyId"
            };
            var message = new TransportMessage(Guid.NewGuid().ToString(), headers);

            ReturnToSenderDequeuer.HandleMessage(message, new FakeBodyStorage(), sender);

            Assert.AreEqual("MessageBodyId", Encoding.UTF8.GetString(sender.Message.Body));
        }

        [Test]
        public void It_uses_retry_to_if_provided()
        {
            var sender = new FakeSender();

            var headers = new Dictionary<string, string>
            {
                ["ServiceControl.Retry.StagingId"] = "SomeId",
                ["ServiceControl.TargetEndpointAddress"] = "TargetEndpoint",
                ["ServiceControl.RetryTo"] = "Proxy"
            };
            var message = new TransportMessage(Guid.NewGuid().ToString(), headers);

            ReturnToSenderDequeuer.HandleMessage(message, new FakeBodyStorage(), sender);

            Assert.AreEqual("Proxy", sender.Options.Destination.Queue);
            Assert.AreEqual("TargetEndpoint", sender.Message.Headers["ServiceControl.TargetEndpointAddress"]);
        }

        [Test]
        public void It_sends_directly_to_target_if_retry_to_is_not_provided()
        {
            var sender = new FakeSender();

            var headers = new Dictionary<string, string>
            {
                ["ServiceControl.Retry.StagingId"] = "SomeId",
                ["ServiceControl.TargetEndpointAddress"] = "TargetEndpoint",
            };
            var message = new TransportMessage(Guid.NewGuid().ToString(), headers);

            ReturnToSenderDequeuer.HandleMessage(message, new FakeBodyStorage(), sender);

            Assert.AreEqual("TargetEndpoint", sender.Options.Destination.Queue);
            Assert.IsFalse(sender.Message.Headers.ContainsKey("ServiceControl.TargetEndpointAddress"));
        }

        [Test]
        public void It_restores_body_id_and_target_addres_after_failure()
        {
            var sender = new FaultySender();

            var headers = new Dictionary<string, string>
            {
                ["ServiceControl.Retry.StagingId"] = "SomeId",
                ["ServiceControl.TargetEndpointAddress"] = "TargetEndpoint",
                ["ServiceControl.Retry.Attempt.MessageId"] = "MessageBodyId",
            };
            var message = new TransportMessage(Guid.NewGuid().ToString(), headers);

            try
            {
                ReturnToSenderDequeuer.HandleMessage(message, new FakeBodyStorage(), sender);
            }
            catch (Exception)
            {
                //Intentionally empty catch
            }
            Assert.IsTrue(message.Headers.ContainsKey("ServiceControl.TargetEndpointAddress"));
            Assert.IsTrue(message.Headers.ContainsKey("ServiceControl.Retry.Attempt.MessageId"));
        }

        class FaultySender : ISendMessages
        {
            public void Send(TransportMessage message, SendOptions sendOptions)
            {
                throw new Exception("Simulated");
            }
        }

        class FakeSender : ISendMessages
        {
            public TransportMessage Message { get; private set; }
            public SendOptions Options { get; private set; }

            public void Send(TransportMessage message, SendOptions sendOptions)
            {
                Message = message;
                Options = sendOptions;
            }
        }

        class FakeBodyStorage : IBodyStorage
        {
            public string Store(string bodyId, string contentType, int bodySize, Stream bodyStream)
            {
                throw new NotImplementedException();
            }

            public bool TryFetch(string bodyId, out Stream stream)
            {
                stream = new MemoryStream(Encoding.UTF8.GetBytes(bodyId)); //Echo back the body ID.
                return true;
            }
        }
    }
}