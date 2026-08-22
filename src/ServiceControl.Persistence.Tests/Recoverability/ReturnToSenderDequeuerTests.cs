namespace ServiceControl.Persistence.Tests.Recoverability
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using EventLog;
    using MessageFailures;
    using MessageFailures.Api;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging.Abstractions;
    using NServiceBus.Extensibility;
    using NServiceBus.Transport;
    using NUnit.Framework;
    using Persistence;
    using Persistence.Infrastructure;
    using ServiceControl.CompositeViews.Messages;
    using ServiceControl.Operations;
    using ServiceControl.Operations.BodyStorage;
    using ServiceControl.Recoverability;

    [TestFixture]
    class ReturnToSenderDequeuerTests : PersistenceTestBase
    {
        MessageContext CreateMessage(string id, Dictionary<string, string> headers) =>
            new(
                id,
                headers,
                ReadOnlyMemory<byte>.Empty,
                new TransportTransaction(),
                "receiveAddress",
                new ContextBag()
            );

        public ReturnToSenderDequeuerTests() => RegisterServices = services => services.AddSingleton<ReturnToSender>();


        [Test]
        public async Task It_removes_staging_id_header()
        {
            var sender = new FakeSender();

            var headers = new Dictionary<string, string>
            {
                ["ServiceControl.Retry.StagingId"] = "SomeId",
                ["ServiceControl.TargetEndpointAddress"] = "TargetEndpoint",
            };
            var message = CreateMessage(Guid.NewGuid().ToString(), headers);

            await new ReturnToSender(null, NullLogger<ReturnToSender>.Instance).HandleMessage(message, sender, "error");

            Assert.That(sender.Message.Headers.ContainsKey("ServiceControl.Retry.StagingId"), Is.False);
        }

        [Test]
        public async Task It_fetches_the_body_from_storage_if_provided()
        {
            var sender = new FakeSender();

            var headers = new Dictionary<string, string>
            {
                ["ServiceControl.Retry.StagingId"] = "SomeId",
                ["ServiceControl.TargetEndpointAddress"] = "TargetEndpoint",
                ["ServiceControl.Retry.Attempt.MessageId"] = "MessageBodyId",
                ["ServiceControl.Retry.UniqueMessageId"] = "MessageBodyId"
            };
            var message = CreateMessage(Guid.NewGuid().ToString(), headers);

            await new ReturnToSender(new FakeBodyStorage(), NullLogger<ReturnToSender>.Instance).HandleMessage(message, sender, "error");

            Assert.That(Encoding.UTF8.GetString(sender.Message.Body.ToArray()), Is.EqualTo("MessageBodyId"));
        }

        [Test]
        public async Task It_sends_an_empty_body_when_storage_reports_empty()
        {
            var sender = new FakeSender();
            var headers = new Dictionary<string, string>
            {
                ["ServiceControl.TargetEndpointAddress"] = "TargetEndpoint",
                ["ServiceControl.Retry.Attempt.MessageId"] = "MessageBodyId",
                ["ServiceControl.Retry.UniqueMessageId"] = "MessageBodyId"
            };
            var message = CreateMessage(Guid.NewGuid().ToString(), headers);

            await new ReturnToSender(new FakeBodyStorage(MessageBodyState.Empty), NullLogger<ReturnToSender>.Instance).HandleMessage(message, sender, "error");

            Assert.That(sender.Message.Body.ToArray(), Is.Empty);
        }

        [TestCase(MessageBodyState.NotFound)]
        [TestCase(MessageBodyState.Unavailable)]
        public void It_does_not_send_when_the_body_cannot_be_retrieved(MessageBodyState state)
        {
            var sender = new FakeSender();
            var headers = new Dictionary<string, string>
            {
                ["ServiceControl.TargetEndpointAddress"] = "TargetEndpoint",
                ["ServiceControl.Retry.Attempt.MessageId"] = "MessageBodyId",
                ["ServiceControl.Retry.UniqueMessageId"] = "MessageBodyId"
            };
            var message = CreateMessage(Guid.NewGuid().ToString(), headers);

            Assert.ThrowsAsync<InvalidOperationException>(() =>
                new ReturnToSender(new FakeBodyStorage(state), NullLogger<ReturnToSender>.Instance).HandleMessage(message, sender, "error"));

            Assert.That(sender.Message, Is.Null);
        }

        [Test]
        public async Task It_uses_retry_to_if_provided()
        {
            var sender = new FakeSender();

            var headers = new Dictionary<string, string>
            {
                ["ServiceControl.Retry.StagingId"] = "SomeId",
                ["ServiceControl.TargetEndpointAddress"] = "TargetEndpoint",
                ["ServiceControl.RetryTo"] = "Proxy",
            };
            var message = CreateMessage(Guid.NewGuid().ToString(), headers);

            await new ReturnToSender(null, NullLogger<ReturnToSender>.Instance).HandleMessage(message, sender, "error");

            using (Assert.EnterMultipleScope())
            {
                Assert.That(sender.Destination, Is.EqualTo("Proxy"));
                Assert.That(sender.Message.Headers["ServiceControl.TargetEndpointAddress"], Is.EqualTo("TargetEndpoint"));
            }
        }

        [Test]
        public async Task It_sends_directly_to_target_if_retry_to_is_not_provided()
        {
            var sender = new FakeSender();

            var headers = new Dictionary<string, string>
            {
                ["ServiceControl.Retry.StagingId"] = "SomeId",
                ["ServiceControl.TargetEndpointAddress"] = "TargetEndpoint",
            };
            var message = CreateMessage(Guid.NewGuid().ToString(), headers);

            await new ReturnToSender(null, NullLogger<ReturnToSender>.Instance).HandleMessage(message, sender, "error");

            using (Assert.EnterMultipleScope())
            {
                Assert.That(sender.Destination, Is.EqualTo("TargetEndpoint"));
                Assert.That(sender.Message.Headers.ContainsKey("ServiceControl.TargetEndpointAddress"), Is.False);
            }
        }

        [Test]
        public async Task It_restores_body_id_and_target_addres_after_failure()
        {
            var sender = new FaultySender();

            var headers = new Dictionary<string, string>
            {
                ["ServiceControl.Retry.StagingId"] = "SomeId",
                ["ServiceControl.TargetEndpointAddress"] = "TargetEndpoint",
                ["ServiceControl.Retry.Attempt.MessageId"] = "MessageBodyId",
            };
            var message = CreateMessage(Guid.NewGuid().ToString(), headers);

            try
            {
                await new ReturnToSender(null, NullLogger<ReturnToSender>.Instance).HandleMessage(message, sender, "error");
            }
            catch (Exception)
            {
                //Intentionally empty catch
            }

            using (Assert.EnterMultipleScope())
            {
                Assert.That(message.Headers.ContainsKey("ServiceControl.TargetEndpointAddress"), Is.True);
                Assert.That(message.Headers.ContainsKey("ServiceControl.Retry.Attempt.MessageId"), Is.True);
            }
        }

        class FaultySender : IMessageDispatcher
        {
            public Task Dispatch(TransportOperations outgoingMessages, TransportTransaction transaction, CancellationToken cancellationToken = default)
            {
                throw new Exception("Simulated");
            }
        }

        class FakeSender : IMessageDispatcher
        {
            public OutgoingMessage Message { get; private set; }
            public string Destination { get; private set; }


            public Task Dispatch(TransportOperations outgoingMessages, TransportTransaction transaction, CancellationToken cancellationToken = default)
            {
                var operation = outgoingMessages.UnicastTransportOperations.Single();
                Message = operation.Message;
                Destination = operation.Destination;
                return Task.CompletedTask;
            }
        }

        class FakeBodyStorage(MessageBodyState state = MessageBodyState.Available) : IBodyStorage
        {
            public Task<MessageBodyResult> TryFetch(string bodyId, CancellationToken cancellationToken = default) =>
                Task.FromResult(state switch
                {
                    MessageBodyState.NotFound => MessageBodyResult.NotFound(),
                    MessageBodyState.Empty => MessageBodyResult.Empty(),
                    MessageBodyState.Unavailable => MessageBodyResult.Unavailable(),
                    MessageBodyState.Available => MessageBodyResult.Available(Content(Encoding.UTF8.GetBytes(bodyId))),
                    _ => throw new ArgumentOutOfRangeException(nameof(state), state, null)
                });

            static MessageBodyStreamContent Content(byte[] body) => new(new MemoryStream(body), "text/plain", body.Length, DataVersion.FromToken("etag"));
        }
    }
}