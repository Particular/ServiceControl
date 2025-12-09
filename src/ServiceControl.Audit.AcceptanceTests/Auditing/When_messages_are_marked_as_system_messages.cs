namespace ServiceControl.Audit.AcceptanceTests.Auditing
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.EndpointTemplates;
    using Audit.Auditing.MessagesView;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Routing;
    using NServiceBus.Transport;
    using NUnit.Framework;

    class When_messages_are_marked_as_system_messages : AcceptanceTest
    {
        [Test]
        public async Task Should_set_the_IsSystemMessage_when_message_type_is_not_a_scheduled_task()
        {
            MessagesView auditMessage = null;
            await Define<SystemMessageTestContext>(ctx =>
                {
                    ctx.MessageId = Guid.NewGuid().ToString();
                    ctx.EnclosedMessageType = "SendOnlyError.SendSomeCommand, TestSendOnlyError, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null";
                    ctx.IncludeControlMessageHeader = false;
                })
                .WithEndpoint<SystemMessageEndpoint>()
                .Done(async c =>
                {
                    var result = await this.TryGetSingle<MessagesView>("/api/messages", r => r.MessageId == c.MessageId);
                    auditMessage = result;
                    return result;
                })
                .Run();

            Assert.That(auditMessage, Is.Not.Null);
            Assert.That(auditMessage.IsSystemMessage, Is.False);
        }

        [Test]
        public async Task Scheduled_task_messages_should_set_IsSystemMessage()
        {
            MessagesView auditMessage = null;
            await Define<SystemMessageTestContext>(ctx =>
                {
                    ctx.MessageId = Guid.NewGuid().ToString();
                    ctx.EnclosedMessageType = "NServiceBus.Scheduling.Messages.ScheduledTask, TestSendOnlyError, Version=5.0.0.0, Culture=neutral, PublicKeyToken=null";
                    ctx.IncludeControlMessageHeader = false;
                })
                .WithEndpoint<SystemMessageEndpoint>()
                .Done(async c =>
                {
                    var result = await this.TryGetSingle<MessagesView>("/api/messages?include_system_messages=true&sort=id", r => r.MessageId == c.MessageId);
                    auditMessage = result;
                    return result;
                })
                .Run();
            Assert.That(auditMessage, Is.Not.Null);
            Assert.That(auditMessage.IsSystemMessage, Is.True);
        }

        [Test]
        public async Task Control_messages_should_not_be_audited()
        {
            var containsItem = true;

            await Define<SystemMessageTestContext>(ctx =>
                {
                    ctx.MessageId = Guid.NewGuid().ToString();
                    ctx.EnclosedMessageType = "NServiceBus.Scheduling.Messages.ScheduledTask";
                    ctx.IncludeControlMessageHeader = true; // If the control message header is present, then its a system message regardless of the value
                    ctx.ControlMessageHeaderValue = null;
                })
                .WithEndpoint<SystemMessageEndpoint>()
                .Done(async c =>
                {
                    if (!c.QueryForMessages)
                    {
                        return false;
                    }

                    // Wait for other parts of the system to work
                    await Task.Delay(250);

                    var result = await this.TryGet<List<MessagesView>>("/api/messages");
                    List<MessagesView> messages = result;
                    if (!result)
                    {
                        return false;
                    }

                    var items = messages.Where(r => r.MessageId == c.MessageId);

                    containsItem = items.Any();

                    return true;
                })
                .Run();

            Assert.That(containsItem, Is.False);
        }

        [Test]
        public async Task Should_set_the_IsSystemMessage_for_integration_scenario()
        {
            MessagesView auditMessage = null;
            await Define<SystemMessageTestContext>(ctx =>
                {
                    ctx.MessageId = Guid.NewGuid().ToString();
                    ctx.EnclosedMessageType = null;
                    ctx.IncludeControlMessageHeader = false;
                })
                .WithEndpoint<SystemMessageEndpoint>()
                .Done(async c =>
                {
                    var result = await this.TryGetSingle<MessagesView>("/api/messages", r => r.MessageId == c.MessageId);
                    auditMessage = result;
                    return result;
                })
                .Run();

            Assert.That(auditMessage, Is.Not.Null);
            Assert.That(auditMessage.IsSystemMessage, Is.False);
        }

        class SystemMessageEndpoint : EndpointConfigurationBuilder
        {
            public SystemMessageEndpoint() => EndpointSetup<DefaultServerWithAudit>(c => c.EnableFeature<SendMessageLowLevel>());

            public class SendMessageLowLevel : DispatchRawMessages<SystemMessageTestContext>
            {
                protected override TransportOperations CreateMessage(SystemMessageTestContext context)
                {
                    // Transport message has no headers for Processing endpoint and the ReplyToAddress is set to null
                    var headers = new Dictionary<string, string>
                    {
                        [Headers.ProcessingEndpoint] = "SystemMessageEndpoint",
                        [Headers.MessageId] = context.MessageId,
                        [Headers.ConversationId] = "a59395ee-ec80-41a2-a728-a3df012fc707",
                        ["$.diagnostics.hostid"] = "bdd4b0510bff5a6d07e91baa7e16a804",
                        ["$.diagnostics.hostdisplayname"] = "SELENE"
                    };

                    if (!string.IsNullOrEmpty(context.EnclosedMessageType))
                    {
                        headers[Headers.EnclosedMessageTypes] = context.EnclosedMessageType;
                    }

                    if (context.IncludeControlMessageHeader)
                    {
                        headers[Headers.ControlMessageHeader] = context.ControlMessageHeaderValue != null && (bool)context.ControlMessageHeaderValue ? context.ControlMessageHeaderValue.ToString() : null;
                    }

                    return new TransportOperations(new TransportOperation(new OutgoingMessage(context.MessageId, headers, Array.Empty<byte>()), new UnicastAddressTag("audit")));
                }

                protected override Task AfterDispatch(IMessageSession session, SystemMessageTestContext context)
                    => session.SendLocal(new DoQueryAllowed());
            }

            class MyHandler(SystemMessageTestContext testContext) : IHandleMessages<DoQueryAllowed>
            {
                public Task Handle(DoQueryAllowed message, IMessageHandlerContext context)
                {
                    testContext.QueryForMessages = true;
                    return Task.CompletedTask;
                }
            }
        }

        public class DoQueryAllowed : IMessage;

        public class SystemMessageTestContext : ScenarioContext
        {
            public string MessageId { get; set; }
            public bool IncludeControlMessageHeader { get; set; }
            public bool? ControlMessageHeaderValue { get; set; }
            public string EnclosedMessageType { get; set; }
            public bool QueryForMessages { get; set; }
        }
    }
}