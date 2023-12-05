﻿namespace ServiceControl.UnitTests.Recoverability
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Contracts.Operations;
    using MessageFailures;
    using Microsoft.Extensions.DependencyInjection;
    using NServiceBus.Testing;
    using NServiceBus.Transport;
    using NUnit.Framework;
    using ServiceControl.Persistence.MessageRedirects;
    using ServiceControl.Recoverability;
    using ServiceControl.Recoverability.Editing;

    sealed class EditMessageTests : PersistenceTestBase
    {
        EditHandler handler;
        readonly TestableUnicastDispatcher dispatcher = new TestableUnicastDispatcher();

        public EditMessageTests()
        {
            RegisterServices = services => services
                .AddSingleton<IMessageDispatcher>(dispatcher)
                .AddTransient<EditHandler>();
        }

        [SetUp]
        public void Setup()
        {
            handler = GetRequiredService<EditHandler>();
        }

        [Test]
        public async Task Should_discard_edit_when_failed_message_not_exists()
        {
            var message = CreateEditMessage("some-id");
            await handler.Handle(message, new TestableMessageHandlerContext());

            Assert.IsEmpty(dispatcher.DispatchedMessages);
        }

        [Test]
        [TestCase(FailedMessageStatus.RetryIssued)]
        [TestCase(FailedMessageStatus.Archived)]
        [TestCase(FailedMessageStatus.Resolved)]
        public async Task Should_discard_edit_if_edited_message_not_unresolved(FailedMessageStatus status)
        {
            var failedMessageId = Guid.NewGuid().ToString("D");
            await CreateAndStoreFailedMessage(failedMessageId, status);

            var message = CreateEditMessage(failedMessageId);
            await handler.Handle(message, new TestableMessageHandlerContext());

            var failedMessage = await ErrorMessageDataStore.ErrorBy(failedMessageId);

            var editFailedMessagesManager = await ErrorMessageDataStore.CreateEditFailedMessageManager();
            var editOperation = await editFailedMessagesManager.GetCurrentEditingMessageId(failedMessageId);

            Assert.AreEqual(status, failedMessage.Status);
            Assert.IsNull(editOperation);

            Assert.IsEmpty(dispatcher.DispatchedMessages);
        }

        [Test]
        public async Task Should_discard_edit_when_different_edit_already_exists()
        {
            var failedMessageId = Guid.NewGuid().ToString();
            var previousEdit = Guid.NewGuid().ToString();

            _ = await CreateAndStoreFailedMessage(failedMessageId);

            using (var editFailedMessagesManager = await ErrorMessageDataStore.CreateEditFailedMessageManager())
            {
                _ = await editFailedMessagesManager.GetFailedMessage(failedMessageId);
                await editFailedMessagesManager.SetCurrentEditingMessageId(previousEdit);
                await editFailedMessagesManager.SaveChanges();
            }

            var message = CreateEditMessage(failedMessageId);

            // Act
            await handler.Handle(message, new TestableMessageHandlerContext());

            using (var editFailedMessagesManagerAssert = await ErrorMessageDataStore.CreateEditFailedMessageManager())
            {
                var failedMessage = await editFailedMessagesManagerAssert.GetFailedMessage(failedMessageId);
                var editId = await editFailedMessagesManagerAssert.GetCurrentEditingMessageId(failedMessageId);

                Assert.AreEqual(previousEdit, editId);
                Assert.AreEqual(FailedMessageStatus.Unresolved, failedMessage.Status);
            }

            Assert.IsEmpty(dispatcher.DispatchedMessages);
        }

        [Test]
        public async Task Should_dispatch_edited_message_when_first_edit()
        {
            var failedMessage = await CreateAndStoreFailedMessage();

            var newBodyContent = Encoding.UTF8.GetBytes("new body content");
            var newHeaders = new Dictionary<string, string> { { "someKey", "someValue" } };
            var message = CreateEditMessage(failedMessage.UniqueMessageId, newBodyContent, newHeaders);

            var handlerContent = new TestableMessageHandlerContext();
            await handler.Handle(message, handlerContent);

            var dispatchedMessage = dispatcher.DispatchedMessages.Single();
            Assert.AreEqual(
                failedMessage.ProcessingAttempts.Last().FailureDetails.AddressOfFailingEndpoint,
                dispatchedMessage.Item1.Destination);
            Assert.AreEqual(newBodyContent, dispatchedMessage.Item1.Message.Body.ToArray());
            Assert.AreEqual("someValue", dispatchedMessage.Item1.Message.Headers["someKey"]);

            using (var x = await ErrorMessageDataStore.CreateEditFailedMessageManager())
            {
                var failedMessage2 = await x.GetFailedMessage(failedMessage.UniqueMessageId);
                Assert.IsNotNull(failedMessage2, "Edited failed message");

                var editId = await x.GetCurrentEditingMessageId(failedMessage2.UniqueMessageId);

                Assert.AreEqual(FailedMessageStatus.Resolved, failedMessage2.Status, "Failed message status");
                Assert.AreEqual(handlerContent.MessageId, editId, "MessageId");
            }
        }

        [Test]
        public async Task Should_dispatch_edited_message_when_retrying()
        {
            var failedMessageId = Guid.NewGuid().ToString();
            await CreateAndStoreFailedMessage(failedMessageId);

            var handlerContext = new TestableMessageHandlerContext();
            var message = CreateEditMessage(failedMessageId);
            await handler.Handle(message, handlerContext);
            await handler.Handle(message, handlerContext);

            Assert.AreEqual(2, dispatcher.DispatchedMessages.Count, "Dispatched message count");
        }

        [Test]
        public async Task Should_dispatch_message_using_incoming_transaction()
        {
            var failedMessage = await CreateAndStoreFailedMessage();
            var message = CreateEditMessage(failedMessage.UniqueMessageId);
            var handlerContent = new TestableMessageHandlerContext();
            var transportTransaction = new TransportTransaction();
            handlerContent.Extensions.Set(transportTransaction);

            await handler.Handle(message, handlerContent);

            Assert.AreSame(dispatcher.DispatchedMessages.Single().Item2, transportTransaction);
        }

        [Test]
        public async Task Should_route_to_redirect_route_if_exists()
        {
            const string redirectAddress = "a different destination";
            var failedMessage = await CreateAndStoreFailedMessage();
            var message = CreateEditMessage(failedMessage.UniqueMessageId);

            var redirects = await MessageRedirectsDataStore.GetOrCreate();
            redirects.Redirects.Add(new MessageRedirect
            {
                FromPhysicalAddress = failedMessage.ProcessingAttempts.Last().FailureDetails.AddressOfFailingEndpoint,
                ToPhysicalAddress = redirectAddress
            });
            await MessageRedirectsDataStore.Save(redirects);

            await handler.Handle(message, new TestableInvokeHandlerContext());

            var sentMessage = dispatcher.DispatchedMessages.Single().Item1;
            Assert.AreEqual(redirectAddress, sentMessage.Destination);
        }

        [Test]
        public async Task Should_mark_edited_message_with_edit_information()
        {
            var messageFailure = await CreateAndStoreFailedMessage();
            var message = CreateEditMessage(messageFailure.UniqueMessageId);

            await handler.Handle(message, new TestableInvokeHandlerContext());

            var sentMessage = dispatcher.DispatchedMessages.Single();
            Assert.AreEqual(
                messageFailure.Id,
                "FailedMessages/" + sentMessage.Item1.Message.Headers["ServiceControl.EditOf"]);
        }

        [Test]
        public async Task Should_assign_edited_message_new_message_id()
        {
            var messageFailure = await CreateAndStoreFailedMessage();
            var message = CreateEditMessage(messageFailure.UniqueMessageId);

            await handler.Handle(message, new TestableInvokeHandlerContext());

            var sentMessage = dispatcher.DispatchedMessages.Single();
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
                NewHeaders = newHeaders ?? []
            };
        }

        async Task<FailedMessage> CreateAndStoreFailedMessage(string failedMessageId = null, FailedMessageStatus status = FailedMessageStatus.Unresolved)
        {
            failedMessageId = failedMessageId ?? Guid.NewGuid().ToString();

            var failedMessage = new FailedMessage
            {
                UniqueMessageId = failedMessageId,
                Id = FailedMessageIdGenerator.MakeDocumentId(failedMessageId),
                Status = status,
                ProcessingAttempts =
                    [
                        new FailedMessage.ProcessingAttempt
                        {
                            MessageId = Guid.NewGuid().ToString(),
                            FailureDetails = new FailureDetails
                            {
                                AddressOfFailingEndpoint = "OriginalEndpointAddress"
                            }
                        }
                    ]
            };
            await ErrorMessageDataStore.StoreFailedMessagesForTestsOnly(new[] { failedMessage });
            return failedMessage;
        }
    }

    public sealed class TestableUnicastDispatcher : IMessageDispatcher
    {
        public List<(UnicastTransportOperation, TransportTransaction)> DispatchedMessages { get; } = [];

        public Task Dispatch(TransportOperations outgoingMessages, TransportTransaction transaction, CancellationToken cancellationToken)
        {
            DispatchedMessages.AddRange(outgoingMessages.UnicastTransportOperations.Select(m => (m, transaction)));
            return Task.CompletedTask;
        }
    }
}