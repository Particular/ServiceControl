namespace ServiceBus.Management.AcceptanceTests.MessageFailures
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Contexts;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Transports;
    using NServiceBus.Unicast;
    using NUnit.Framework;
    using ServiceControl.CompositeViews.Messages;

    class Audit_Messages_Have_Proper_IsSystemMessage_Tests: AcceptanceTest
    {
        [Test]
        public async Task Should_set_the_IsSystemMessage_when_message_type_is_not_a_scheduled_task()
        {
            var context = new SystemMessageTestContext
            {
                MessageId = Guid.NewGuid().ToString(),
                EnclosedMessageType = "SendOnlyError.SendSomeCommand, TestSendOnlyError, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null",
                IncludeControlMessageHeader = false,
            };

            MessagesView auditMessage = null;
            await Define(context)
                .WithEndpoint<ServerEndpoint>()
                .Done(async c =>
                {
                    var result = await TryGetSingle<MessagesView>("/api/messages", r => r.MessageId == c.MessageId);
                    auditMessage = result;
                    return result;
                })
                .Run();

            Assert.IsNotNull(auditMessage);
            Assert.IsFalse(auditMessage.IsSystemMessage);
        }

        [Test]
        public async Task Scheduled_task_messages_should_set_IsSystemMessage()
        {
            var context = new SystemMessageTestContext
            {
                MessageId = Guid.NewGuid().ToString(),
                EnclosedMessageType = "NServiceBus.Scheduling.Messages.ScheduledTask, TestSendOnlyError, Version=5.0.0.0, Culture=neutral, PublicKeyToken=null",
                IncludeControlMessageHeader = false,
            };

            MessagesView auditMessage = null;
            await Define(context)
                .WithEndpoint<ServerEndpoint>()
                .Done(async c =>
                {
                    var result = await TryGetSingle<MessagesView>("/api/messages?include_system_messages=true&sort=id", r => r.MessageId == c.MessageId);
                    auditMessage = result;
                    return result;
                })
                .Run();
            Assert.IsNotNull(auditMessage);
            Assert.IsTrue(auditMessage.IsSystemMessage);
        }

        [Test]
        public async Task Control_messages_should_not_be_audited()
        {
            var context = new SystemMessageTestContext
            {
                MessageId = Guid.NewGuid().ToString(),
                EnclosedMessageType = "NServiceBus.Scheduling.Messages.ScheduledTask",
                IncludeControlMessageHeader = true, // If the control message header is present, then its a system message regardless of the value
                ControlMessageHeaderValue = null
            };

            var containsItem = true;

            await Define(context)
                .WithEndpoint<ServerEndpoint>()
                .Done(async c =>
                {
                    if (!c.QueryForMessages)
                    {
                        return false;
                    }

                    var result = await TryGet<List<MessagesView>>("/api/messages");
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

            Assert.IsFalse(containsItem);
        }

        [Test]
        public async Task Should_set_the_IsSystemMessage_for_integration_scenario()
        {
            var context = new SystemMessageTestContext
            {
                MessageId = Guid.NewGuid().ToString(),
                EnclosedMessageType = null,
                IncludeControlMessageHeader = false
            };

            MessagesView auditMessage = null;
            await Define(context)
                .WithEndpoint<ServerEndpoint>()
                .Done(async c =>
                {
                    var result = await TryGetSingle<MessagesView>("/api/messages", r => r.MessageId == c.MessageId);
                    auditMessage = result;
                    return result;
                })
                .Run();

            Assert.IsNotNull(auditMessage);
            Assert.IsFalse(auditMessage.IsSystemMessage);
        }

        public class ServerEndpoint : EndpointConfigurationBuilder
        {
            public ServerEndpoint()
            {
                EndpointSetup<DefaultServerWithAudit>();
            }

            class SendMessageLowLevel : IWantToRunWhenBusStartsAndStops
            {
                public ISendMessages SendMessages { get; set; }

                public SystemMessageTestContext SystemMessageTestContext { get; set; }

                public IBus Bus { get; set; }

                public void Start()
                {
                    // Transport message has no headers for Processing endpoint and the ReplyToAddress is set to null
                    var transportMessage = new TransportMessage();
                    transportMessage.Headers[Headers.ProcessingEndpoint] = "ServerEndpoint";
                    transportMessage.Headers[Headers.MessageId] = SystemMessageTestContext.MessageId;
                    transportMessage.Headers[Headers.ConversationId] = "a59395ee-ec80-41a2-a728-a3df012fc707";
                    transportMessage.Headers["$.diagnostics.hostid"] = "bdd4b0510bff5a6d07e91baa7e16a804";
                    transportMessage.Headers["$.diagnostics.hostdisplayname"] = "SELENE";
                    if (!string.IsNullOrEmpty(SystemMessageTestContext.EnclosedMessageType))
                    {
                        transportMessage.Headers[Headers.EnclosedMessageTypes] = SystemMessageTestContext.EnclosedMessageType;
                    }
                    if (SystemMessageTestContext.IncludeControlMessageHeader)
                    {
                        transportMessage.Headers[Headers.ControlMessageHeader] = SystemMessageTestContext.ControlMessageHeaderValue != null && (bool) SystemMessageTestContext.ControlMessageHeaderValue ? SystemMessageTestContext.ControlMessageHeaderValue.ToString() : null;
                    }

                    SendMessages.Send(transportMessage, new SendOptions(Address.Parse("audit")));

                    Bus.SendLocal(new DoQueryAllowed());
                }

                public void Stop()
                {
                }
            }

            class MyHandler : IHandleMessages<DoQueryAllowed>
            {
                public SystemMessageTestContext Context { get; set; }

                public void Handle(DoQueryAllowed message)
                {
                    Context.QueryForMessages = true;
                }
            }
        }

        public class MyMessage : IMessage
        {
        }

        public class DoQueryAllowed : IMessage
        {
        }

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
