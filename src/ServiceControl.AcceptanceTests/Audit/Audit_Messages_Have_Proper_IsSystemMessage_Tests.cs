﻿namespace ServiceBus.Management.AcceptanceTests.MessageFailures
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
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
        public void Should_set_the_IsSystemMessage_when_message_type_is_not_a_scheduled_task()
        {
            var context = new SystemMessageTestContext
            {
                MessageId = Guid.NewGuid().ToString(),
                EnclosedMessageType = "SendOnlyError.SendSomeCommand, TestSendOnlyError, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null",
                IncludeControlMessageHeader = false,
            };
            
            MessagesView auditMessage = null;
            Define(context)
                .WithEndpoint<ManagementEndpoint>(c => c.AppConfig(PathToAppConfig))
                .WithEndpoint<ServerEndpoint>()
                .Done(c => TryGetSingle("/api/messages", out auditMessage, r => r.MessageId == c.MessageId))
                .Run();
            Assert.IsNotNull(auditMessage);
            Assert.IsFalse(auditMessage.IsSystemMessage);
        }

        [Test]
        public void Scheduled_task_messages_should_set_IsSystemMessage()
        {
            var context = new SystemMessageTestContext
            {
                MessageId = Guid.NewGuid().ToString(),
                EnclosedMessageType = "NServiceBus.Scheduling.Messages.ScheduledTask, TestSendOnlyError, Version=5.0.0.0, Culture=neutral, PublicKeyToken=null",
                IncludeControlMessageHeader = false,
            };

            MessagesView auditMessage = null;
            Define(context)
                .WithEndpoint<ManagementEndpoint>(c => c.AppConfig(PathToAppConfig))
                .WithEndpoint<ServerEndpoint>()
                .Done(c => TryGetSingle("/api/messages?include_system_messages=true&sort=id", out auditMessage, r => r.MessageId == c.MessageId))
                .Run();
            Assert.IsNotNull(auditMessage);
            Assert.IsTrue(auditMessage.IsSystemMessage);
        }

        [Test]
        public void Control_messages_should_not_be_audited()
        {
            var context = new SystemMessageTestContext
            {
                MessageId = Guid.NewGuid().ToString(),
                EnclosedMessageType = "NServiceBus.Scheduling.Messages.ScheduledTask",
                IncludeControlMessageHeader = true, // If the control message header is present, then its a system message regardless of the value
                ControlMessageHeaderValue = null
            };

            var containsItem = true;

            Define(context)
                .WithEndpoint<ManagementEndpoint>(c => c.AppConfig(PathToAppConfig))
                .WithEndpoint<ServerEndpoint>()
                .Done(c =>
                {
                    var response = Get<List<MessagesView>>("/api/messages");

                    if (response == null)
                    {
                        Thread.Sleep(1000);

                        return false;
                    }

                    var items = response.Where(r => r.MessageId == c.MessageId);

                    containsItem = items.Any();

                    return true;
                })
                .Run();

            Assert.IsFalse(containsItem);
        }

        [Test]
        public void Should_set_the_IsSystemMessage_for_integration_scenario()
        {
            var context = new SystemMessageTestContext
            {
                MessageId = Guid.NewGuid().ToString(),
                EnclosedMessageType = null,
                IncludeControlMessageHeader = false
            };

            MessagesView auditMessage = null;
            Define(context)
                .WithEndpoint<ManagementEndpoint>(c => c.AppConfig(PathToAppConfig))
                .WithEndpoint<ServerEndpoint>()
                .Done(c => TryGetSingle("/api/messages", out auditMessage, r => r.MessageId == c.MessageId))
                .Run();
            Assert.IsNotNull(auditMessage);
            Assert.IsFalse(auditMessage.IsSystemMessage);
        }

        public class ServerEndpoint : EndpointConfigurationBuilder
        {
            public ServerEndpoint()
            {
                EndpointSetup<DefaultServer>();
            }

            class SendMessageLowLevel : IWantToRunWhenBusStartsAndStops
            {
                public ISendMessages SendMessages { get; set; }

                public SystemMessageTestContext SystemMessageTestContext { get; set; }

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
                }

                public void Stop()
                {
                }
            }
        }

        public class MyMessage : IMessage
        {
        }

        public class SystemMessageTestContext : ScenarioContext
        {
            public string MessageId { get; set; }
            public bool IncludeControlMessageHeader { get; set; }
            public bool? ControlMessageHeaderValue { get; set; }
            public string EnclosedMessageType { get; set; }
        }
 
    }
}
