namespace ServiceControl.UnitTests.Recoverability
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Contracts.Operations;
    using MessageFailures;
    using MessageRedirects;
    using NServiceBus.Extensibility;
    using NServiceBus.Testing;
    using NServiceBus.Transport;
    using NUnit.Framework;
    using Raven.Client.Documents;
    using Raven.TestDriver;
    using ServiceControl.Recoverability;
    using ServiceControl.Recoverability.Editing;

    [TestFixture]
    public class EditMessageTests : RavenTestDriver
    {
        IDocumentStore Store { get; set; }
        EditHandler Handler { get; set; }
        TestableUnicastDispatcher Dispatcher { get; set; }

        [SetUp]
        public void Setup()
        {
            Store = GetDocumentStore();
            Dispatcher = new TestableUnicastDispatcher();
            Handler = new EditHandler(Store, Dispatcher);
        }

        [TearDown]
        public void Teardown()
        {
            Store.Dispose();
        }

        [Test]
        public async Task Should_discard_edit_when_failed_message_not_exists()
        {
            var message = CreateEditMessage("some-id");
            await Handler.Handle(message, new TestableMessageHandlerContext());

            Assert.IsEmpty(Dispatcher.DispatchedMessages);
        }

        [Test]
        [TestCase(FailedMessageStatus.RetryIssued)]
        [TestCase(FailedMessageStatus.Archived)]
        [TestCase(FailedMessageStatus.Resolved)]
        public async Task Should_discard_edit_if_edited_message_not_unresolved(FailedMessageStatus status)
        {
            var failedMessageId = Guid.NewGuid().ToString("D");
            await CreateFailedMessage(failedMessageId, status);

            var message = CreateEditMessage(failedMessageId);
            await Handler.Handle(message, new TestableMessageHandlerContext());

            using (var session = Store.OpenAsyncSession())
            {
                var failedMessage = await session.LoadAsync<FailedMessage>(FailedMessage.MakeDocumentId(failedMessageId));
                var editOperation = await session.LoadAsync<FailedMessageEdit>(failedMessageId);

                Assert.AreEqual(status, failedMessage.Status);
                Assert.IsNull(editOperation);
            }

            Assert.IsEmpty(Dispatcher.DispatchedMessages);
        }

        [Test]
        public async Task Should_discard_edit_when_different_edit_already_exists()
        {
            var failedMessageId = Guid.NewGuid().ToString();
            var previousEdit = Guid.NewGuid().ToString();

            await CreateFailedMessage(failedMessageId);

            using (var session = Store.OpenAsyncSession())
            {
                await session.StoreAsync(new FailedMessageEdit
                {
                    Id = FailedMessageEdit.MakeDocumentId(failedMessageId),
                    FailedMessageId = failedMessageId,
                    EditId = previousEdit
                });

                await session.SaveChangesAsync();
            }

            var message = CreateEditMessage(failedMessageId);
            await Handler.Handle(message, new TestableMessageHandlerContext());

            using (var session = Store.OpenAsyncSession())
            {
                var failedMessage = await session.LoadAsync<FailedMessage>(FailedMessage.MakeDocumentId(failedMessageId));
                var editOperation = await session.LoadAsync<FailedMessageEdit>(FailedMessageEdit.MakeDocumentId(failedMessageId));

                Assert.AreEqual(FailedMessageStatus.Unresolved, failedMessage.Status);
                Assert.AreEqual(previousEdit, editOperation.EditId);
            }

            Assert.IsEmpty(Dispatcher.DispatchedMessages);
        }

        [Test]
        public async Task Should_dispatch_edited_message_when_first_edit()
        {
            var failedMessage = await CreateFailedMessage();

            var newBodyContent = Encoding.UTF8.GetBytes("new body content");
            var newHeaders = new Dictionary<string, string> {{"someKey", "someValue"}};
            var message = CreateEditMessage(failedMessage.UniqueMessageId, newBodyContent, newHeaders);

            var handlerContent = new TestableMessageHandlerContext();
            await Handler.Handle(message, handlerContent);

            var dispatchedMessage = Dispatcher.DispatchedMessages.Single();
            Assert.AreEqual(
                failedMessage.ProcessingAttempts.Last().FailureDetails.AddressOfFailingEndpoint,
                dispatchedMessage.Item1.Destination);
            Assert.AreEqual(newBodyContent, dispatchedMessage.Item1.Message.Body);
            Assert.AreEqual("someValue", dispatchedMessage.Item1.Message.Headers["someKey"]);
            using (var session = Store.OpenAsyncSession())
            {
                failedMessage = await session.LoadAsync<FailedMessage>(failedMessage.Id);
                var editOperation = await session.LoadAsync<FailedMessageEdit>(FailedMessageEdit.MakeDocumentId(failedMessage.UniqueMessageId));

                Assert.AreEqual(FailedMessageStatus.Resolved, failedMessage.Status);
                Assert.AreEqual(handlerContent.MessageId, editOperation.EditId);
            }
        }

        [Test]
        public async Task Should_dispatch_edited_message_when_retrying()
        {
            var failedMessageId = Guid.NewGuid().ToString();
            await CreateFailedMessage(failedMessageId);

            var handlerContext = new TestableMessageHandlerContext();
            var message = CreateEditMessage(failedMessageId);
            await Handler.Handle(message, handlerContext);
            await Handler.Handle(message, handlerContext);

            Assert.AreEqual(2, Dispatcher.DispatchedMessages.Count);
        }

        [Test]
        public async Task Should_dispatch_message_using_incoming_transaction()
        {
            var failedMessage = await CreateFailedMessage();
            var message = CreateEditMessage(failedMessage.UniqueMessageId);
            var handlerContent = new TestableMessageHandlerContext();
            var transportTransaction = new TransportTransaction();
            handlerContent.Extensions.Set(transportTransaction);

            await Handler.Handle(message, handlerContent);

            Assert.AreSame(Dispatcher.DispatchedMessages.Single().Item2, transportTransaction);
        }

        [Test]
        public async Task Should_route_to_redirect_route_if_exists()
        {
            const string redirectAddress = "a different destination";
            var failedMessage = await CreateFailedMessage();
            var message = CreateEditMessage(failedMessage.UniqueMessageId);

            using (var session = Store.OpenAsyncSession())
            {
                var redirects = await MessageRedirectsCollection.GetOrCreate(session);
                redirects.Redirects.Add(new MessageRedirect
                {
                    FromPhysicalAddress = failedMessage.ProcessingAttempts.Last().FailureDetails.AddressOfFailingEndpoint,
                    ToPhysicalAddress = redirectAddress
                });
                await redirects.Save(session);
            }

            await Handler.Handle(message, new TestableInvokeHandlerContext());

            var sentMessage = Dispatcher.DispatchedMessages.Single().Item1;
            Assert.AreEqual(redirectAddress, sentMessage.Destination);
        }

        [Test]
        public async Task Should_mark_edited_message_with_edit_information()
        {
            var messageFailure = await CreateFailedMessage();
            var message = CreateEditMessage(messageFailure.UniqueMessageId);

            await Handler.Handle(message, new TestableInvokeHandlerContext());

            var sentMessage = Dispatcher.DispatchedMessages.Single();
            Assert.AreEqual(
                messageFailure.Id,
                "FailedMessages/" + sentMessage.Item1.Message.Headers["ServiceControl.EditOf"]);
        }

        [Test]
        public async Task Should_assign_edited_message_new_message_id()
        {
            var messageFailure = await CreateFailedMessage();
            var message = CreateEditMessage(messageFailure.UniqueMessageId);

            await Handler.Handle(message, new TestableInvokeHandlerContext());

            var sentMessage = Dispatcher.DispatchedMessages.Single();
            Assert.AreNotEqual(
                messageFailure.ProcessingAttempts.Last().MessageId,
                sentMessage.Item1.Message.MessageId);
        }

        static EditAndSend CreateEditMessage(string failedMessageId, byte[] newBodyContent = null, Dictionary<string, string> newHeaders = null)
        {
            return new EditAndSend
            {
                FailedMessageId = failedMessageId,
                NewBody = Convert.ToBase64String(newBodyContent ?? Encoding.UTF8.GetBytes(Guid.NewGuid().ToString())),
                NewHeaders = newHeaders ?? new Dictionary<string, string>()
            };
        }

        async Task<FailedMessage> CreateFailedMessage(string failedMessageId = null, FailedMessageStatus status = FailedMessageStatus.Unresolved)
        {
            failedMessageId = failedMessageId ?? Guid.NewGuid().ToString();
            using (var session = Store.OpenAsyncSession())
            {
                var failedMessage = new FailedMessage
                {
                    UniqueMessageId = failedMessageId,
                    Id = FailedMessage.MakeDocumentId(failedMessageId),
                    Status = status,
                    ProcessingAttempts = new List<FailedMessage.ProcessingAttempt>
                    {
                        new FailedMessage.ProcessingAttempt
                        {
                            MessageId = Guid.NewGuid().ToString(),
                            FailureDetails = new FailureDetails
                            {
                                AddressOfFailingEndpoint = "OriginalEndpointAddress"
                            }
                        }
                    }
                };
                await session.StoreAsync(failedMessage);
                await session.SaveChangesAsync();
                return failedMessage;
            }
        }
    }

    public class TestableUnicastDispatcher : IDispatchMessages
    {
        public List<(UnicastTransportOperation, TransportTransaction, ContextBag)> DispatchedMessages { get; set; } = new List<(UnicastTransportOperation, TransportTransaction, ContextBag)>();

        public Task Dispatch(TransportOperations outgoingMessages, TransportTransaction transaction, ContextBag context)
        {
            DispatchedMessages.AddRange(outgoingMessages.UnicastTransportOperations.Select(m => (m, transaction, context)));
            return Task.CompletedTask;
        }
    }
}