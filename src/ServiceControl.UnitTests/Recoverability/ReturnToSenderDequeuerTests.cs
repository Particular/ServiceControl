﻿namespace ServiceControl.UnitTests.Operations
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using MessageFailures;
    using NServiceBus.Extensibility;
    using NServiceBus.Transport;
    using NUnit.Framework;
    using ServiceControl.CompositeViews.Messages;
    using ServiceControl.Operations.BodyStorage;
    using ServiceControl.Recoverability;

    [TestFixture]
    public class ReturnToSenderDequeuerTests
    {
        MessageContext CreateMessage(string id, Dictionary<string, string> headers)
        {
            return new MessageContext(
                id,
                headers,
                new byte[0],
                new TransportTransaction(),
                new CancellationTokenSource(),
                new ContextBag()
            );
        }

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

            await new ReturnToSender(new FakeBodyStorage(), null).HandleMessage(message, sender, "error")
                .ConfigureAwait(false);

            Assert.IsFalse(sender.Message.Headers.ContainsKey("ServiceControl.Retry.StagingId"));
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
            };
            var message = CreateMessage(Guid.NewGuid().ToString(), headers);

            await new ReturnToSender(new FakeBodyStorage(), null).HandleMessage(message, sender, "error")
                .ConfigureAwait(false);

            Assert.AreEqual("MessageBodyId", Encoding.UTF8.GetString(sender.Message.Body));
        }

        [Test]
        public async Task It_fetches_the_body_from_index_if_provided()
        {
            var sender = new FakeSender();

            var headers = new Dictionary<string, string>
            {
                ["ServiceControl.Retry.StagingId"] = "SomeId",
                ["ServiceControl.TargetEndpointAddress"] = "TargetEndpoint",
                ["ServiceControl.Retry.Attempt.MessageId"] = "MessageBodyId",
                ["ServiceControl.Retry.UniqueMessageId"] = "MessageBodyId",
                ["ServiceControl.Retry.BodyOnFailedMessage"] = null
            };
            var message = CreateMessage(Guid.NewGuid().ToString(), headers);

            using (var documentStore = InMemoryStoreBuilder.GetInMemoryStore())
            {
                using (var session = documentStore.OpenAsyncSession())
                {
                    await session.StoreAsync(new FailedMessage
                    {
                        Id = FailedMessage.MakeDocumentId("MessageBodyId"),
                        ProcessingAttempts = new List<FailedMessage.ProcessingAttempt>
                        {
                            new FailedMessage.ProcessingAttempt
                            {
                                MessageId = "MessageBodyId",
                                MessageMetadata = new Dictionary<string, object>
                                {
                                    { "Body", "MessageBodyId" }
                                }
                            }
                        }
                    });

                    await session.SaveChangesAsync();
                }

                var transformer = new MessagesBodyTransformer();
                await transformer.ExecuteAsync(documentStore);

                documentStore.WaitForIndexing();

                await new ReturnToSender(null, documentStore).HandleMessage(message, sender, "error")
                    .ConfigureAwait(false);

                Assert.AreEqual("MessageBodyId", Encoding.UTF8.GetString(sender.Message.Body));
            }
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

            await new ReturnToSender(new FakeBodyStorage(), null).HandleMessage(message, sender, "error")
                .ConfigureAwait(false);

            Assert.AreEqual("Proxy", sender.Destination);
            Assert.AreEqual("TargetEndpoint", sender.Message.Headers["ServiceControl.TargetEndpointAddress"]);
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

            await new ReturnToSender(new FakeBodyStorage(), null).HandleMessage(message, sender, "error")
                .ConfigureAwait(false);

            Assert.AreEqual("TargetEndpoint", sender.Destination);
            Assert.IsFalse(sender.Message.Headers.ContainsKey("ServiceControl.TargetEndpointAddress"));
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
                await new ReturnToSender(new FakeBodyStorage(), null).HandleMessage(message, sender, "error")
                    .ConfigureAwait(false);
            }
            catch (Exception)
            {
                //Intentionally empty catch
            }

            Assert.IsTrue(message.Headers.ContainsKey("ServiceControl.TargetEndpointAddress"));
            Assert.IsTrue(message.Headers.ContainsKey("ServiceControl.Retry.Attempt.MessageId"));
        }

        class FaultySender : IDispatchMessages
        {
            public Task Dispatch(TransportOperations outgoingMessages, TransportTransaction transaction, ContextBag context)
            {
                throw new Exception("Simulated");
            }
        }

        class FakeSender : IDispatchMessages
        {
            public OutgoingMessage Message { get; private set; }
            public string Destination { get; private set; }


            public Task Dispatch(TransportOperations outgoingMessages, TransportTransaction transaction, ContextBag context)
            {
                var operation = outgoingMessages.UnicastTransportOperations.Single();
                Message = operation.Message;
                Destination = operation.Destination;
                return Task.FromResult(0);
            }
        }

        class FakeBodyStorage : IBodyStorage
        {
            public Task Store(string bodyId, string contentType, int bodySize, Stream bodyStream)
            {
                throw new NotImplementedException();
            }

            public Task<StreamResult> TryFetch(string bodyId)
            {
                var stream = new MemoryStream(Encoding.UTF8.GetBytes(bodyId)); //Echo back the body ID.
                return Task.FromResult(new StreamResult
                {
                    HasResult = true,
                    Stream = stream
                });
            }
        }
    }
}