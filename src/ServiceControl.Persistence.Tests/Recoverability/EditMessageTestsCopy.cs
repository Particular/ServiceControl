namespace ServiceControl.Persistence.Tests.Recoverability
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
    using NServiceBus.Transport;
    using NUnit.Framework;
    using ServiceControl.Recoverability;
    using ServiceControl.Recoverability.Editing;

    sealed class EditMessageTestsCopy : PersistenceTestBase
    {
        EditHandlerCopy handler;
        readonly TestableUnicastDispatcherCopy dispatcher = new();
        readonly ErrorQueueNameCache errorQueueNameCache = new()
        {
            ResolvedErrorAddress = "errorQueueName"
        };

        public EditMessageTestsCopy() =>
            RegisterServices = services => services
                .AddSingleton<IMessageDispatcher>(dispatcher)
                .AddSingleton(errorQueueNameCache)
                .AddTransient<EditHandlerCopy>();

        [SetUp]
        public void Setup() => handler = ServiceProvider.GetRequiredService<EditHandlerCopy>();

        [Test]
        public async Task Should_discard_edit_when_failed_message_not_exists()
        {
            var message = CreateEditMessage("some-id");
            await handler.Handle(message, message.FailedMessageId);

            Assert.That(dispatcher.DispatchedMessages, Is.Empty);
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
            await handler.Handle(message, message.FailedMessageId);

            var failedMessage = await ErrorMessageDataStore.ErrorBy(failedMessageId);

            var editFailedMessagesManager = await ErrorMessageDataStore.CreateEditFailedMessageManager();
            var editOperation = await editFailedMessagesManager.GetCurrentEditingMessageId(failedMessageId);

            Assert.Multiple(() =>
            {
                Assert.That(failedMessage.Status, Is.EqualTo(status));
                Assert.That(editOperation, Is.Null);
                Assert.That(dispatcher.DispatchedMessages, Is.Empty);
            });
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
            await handler.Handle(message, message.FailedMessageId);

            using (var editFailedMessagesManagerAssert = await ErrorMessageDataStore.CreateEditFailedMessageManager())
            {
                var failedMessage = await editFailedMessagesManagerAssert.GetFailedMessage(failedMessageId);
                var editId = await editFailedMessagesManagerAssert.GetCurrentEditingMessageId(failedMessageId);

                Assert.Multiple(() =>
                {
                    Assert.That(editId, Is.EqualTo(previousEdit));
                    Assert.That(failedMessage.Status, Is.EqualTo(FailedMessageStatus.Unresolved));
                });
            }

            Assert.That(dispatcher.DispatchedMessages, Is.Empty);
        }

        [Test]
        public async Task Should_dispatch_edited_message_when_first_edit()
        {
            var failedMessage = await CreateAndStoreFailedMessage();

            var newBodyContent = Encoding.UTF8.GetBytes("new body content");
            var newHeaders = new Dictionary<string, string> { { "someKey", "someValue" } };
            var message = CreateEditMessage(failedMessage.UniqueMessageId, newBodyContent, newHeaders);

            await handler.Handle(message, message.FailedMessageId);

            var dispatchedMessage = dispatcher.DispatchedMessages.Single();
            Assert.Multiple(() =>
            {
                Assert.That(
                            dispatchedMessage.Item1.Destination,
                            Is.EqualTo(failedMessage.ProcessingAttempts.Last().FailureDetails.AddressOfFailingEndpoint));
                Assert.That(dispatchedMessage.Item1.Message.Body.ToArray(), Is.EqualTo(newBodyContent));
                Assert.That(dispatchedMessage.Item1.Message.Headers["someKey"], Is.EqualTo("someValue"));
            });

            using (var x = await ErrorMessageDataStore.CreateEditFailedMessageManager())
            {
                var failedMessage2 = await x.GetFailedMessage(failedMessage.UniqueMessageId);
                Assert.That(failedMessage2, Is.Not.Null, "Edited failed message");

                var editId = await x.GetCurrentEditingMessageId(failedMessage2.UniqueMessageId);

                Assert.Multiple(() =>
                {
                    Assert.That(failedMessage2.Status, Is.EqualTo(FailedMessageStatus.Resolved), "Failed message status");
                    Assert.That(editId, Is.EqualTo(message.FailedMessageId), "MessageId");
                });
            }
        }

        [Test]
        public async Task Should_dispatch_edited_message_when_retrying()
        {
            var failedMessageId = Guid.NewGuid().ToString();
            var controlMessageId = Guid.NewGuid().ToString();
            await CreateAndStoreFailedMessage(failedMessageId);

            var message = CreateEditMessage(failedMessageId);
            await handler.Handle(message, controlMessageId);
            await handler.Handle(message, controlMessageId);

            Assert.That(dispatcher.DispatchedMessages, Has.Count.EqualTo(2), "Dispatched message count");
        }

        [Test]
        public async Task Simulate_Two_Tabs_dispatching_two_control_messages_for_the_same_failed_message()
        {
            var failedMessageId = Guid.NewGuid().ToString();
            await CreateAndStoreFailedMessage(failedMessageId);

            var message = CreateEditMessage(failedMessageId);
            await handler.Handle(message, Guid.NewGuid().ToString());
            await handler.Handle(message, Guid.NewGuid().ToString());

            Assert.That(dispatcher.DispatchedMessages, Has.Count.EqualTo(1), "Dispatched message count");
        }

        //[Test]
        //public async Task Should_dispatch_message_using_incoming_transaction()
        //{
        //    var failedMessage = await CreateAndStoreFailedMessage();
        //    var message = CreateEditMessage(failedMessage.UniqueMessageId);
        //    var handlerContent = new TestableMessageHandlerContext();
        //    var transportTransaction = new TransportTransaction();
        //    handlerContent.Extensions.Set(transportTransaction);

        //    await handler.Handle(message, handlerContent);

        //    Assert.That(transportTransaction, Is.SameAs(dispatcher.DispatchedMessages.Single().Item2));
        //}

        //[Test]
        //public async Task Should_route_to_redirect_route_if_exists()
        //{
        //    const string redirectAddress = "a different destination";
        //    var failedMessage = await CreateAndStoreFailedMessage();
        //    var message = CreateEditMessage(failedMessage.UniqueMessageId);

        //    var redirects = await MessageRedirectsDataStore.GetOrCreate();
        //    redirects.Redirects.Add(new MessageRedirect
        //    {
        //        FromPhysicalAddress = failedMessage.ProcessingAttempts.Last().FailureDetails.AddressOfFailingEndpoint,
        //        ToPhysicalAddress = redirectAddress
        //    });
        //    await MessageRedirectsDataStore.Save(redirects);

        //    await handler.Handle(message, new TestableInvokeHandlerContext());

        //    var sentMessage = dispatcher.DispatchedMessages.Single().Item1;
        //    Assert.That(sentMessage.Destination, Is.EqualTo(redirectAddress));
        //}

        //[Test]
        //public async Task Should_mark_edited_message_with_edit_information()
        //{
        //    var messageFailure = await CreateAndStoreFailedMessage();
        //    var message = CreateEditMessage(messageFailure.UniqueMessageId);

        //    await handler.Handle(message, new TestableInvokeHandlerContext());

        //    var sentMessage = dispatcher.DispatchedMessages.Single();
        //    Assert.That(
        //        "FailedMessages/" + sentMessage.Item1.Message.Headers["ServiceControl.EditOf"],
        //        Is.EqualTo(messageFailure.Id));
        //}

        //[Test]
        //public async Task Should_assign_edited_message_new_message_id()
        //{
        //    var messageFailure = await CreateAndStoreFailedMessage();
        //    var message = CreateEditMessage(messageFailure.UniqueMessageId);

        //    await handler.Handle(message, new TestableInvokeHandlerContext());

        //    var sentMessage = dispatcher.DispatchedMessages.Single();
        //    Assert.That(
        //        sentMessage.Item1.Message.MessageId,
        //        Is.Not.EqualTo(messageFailure.ProcessingAttempts.Last().MessageId));
        //}

        //[Test]
        //public async Task Should_assign_correct_akcnowledgment_queue_address_when_editing_and_retyring()
        //{
        //    var messageFailure = await CreateAndStoreFailedMessage();
        //    var message = CreateEditMessage(messageFailure.UniqueMessageId);

        //    await handler.Handle(message, new TestableInvokeHandlerContext());

        //    var sentMessage = dispatcher.DispatchedMessages.Single();
        //    Assert.That(
        //        sentMessage.Item1.Message.Headers["ServiceControl.Retry.AcknowledgementQueue"],
        //        Is.EqualTo(errorQueueNameCache.ResolvedErrorAddress));
        //}

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
            failedMessageId ??= Guid.NewGuid().ToString();

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

    public sealed class TestableUnicastDispatcherCopy : IMessageDispatcher
    {
        public List<(UnicastTransportOperation, TransportTransaction)> DispatchedMessages { get; } = [];

        public Task Dispatch(TransportOperations outgoingMessages, TransportTransaction transaction, CancellationToken cancellationToken)
        {
            DispatchedMessages.AddRange(outgoingMessages.UnicastTransportOperations.Select(m => (m, transaction)));
            return Task.CompletedTask;
        }
    }
}