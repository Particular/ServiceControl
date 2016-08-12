namespace ServiceBus.Management.AcceptanceTests.MessageFailures
{
    using System;
    using Contexts;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Transports;
    using NServiceBus.Unicast;
    using NUnit.Framework;
    using ServiceControl.MessageFailures.Api;

    class Is_System_Message_Tests: AcceptanceTest
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

            FailedMessageView failure = null;
            Define(context)
                .WithEndpoint<ServerEndpoint>()
                .Done(c => TryGetSingle("/api/errors", out failure, r => r.MessageId == c.MessageId))
                .Run();
            Assert.IsNotNull(failure);
            Assert.IsFalse(failure.IsSystemMessage);
        }

        [Test]
        public void Should_set_the_IsSystemMessage_when_message_type_is_a_scheduled_task()
        {
            var context = new SystemMessageTestContext
            {
                MessageId = Guid.NewGuid().ToString(),
                EnclosedMessageType = "NServiceBus.Scheduling.Messages.ScheduledTask",
                IncludeControlMessageHeader = false,
            };

            FailedMessageView failure = null;
            Define(context)
                .WithEndpoint<ServerEndpoint>()
                .Done(c => TryGetSingle("/api/errors", out failure, r => r.MessageId == c.MessageId))
                .Run();
            Assert.IsNotNull(failure);
            Assert.IsTrue(failure.IsSystemMessage);
        }

        [Test]
        public void Should_set_the_IsSystemMessage_when_control_message_header_is_true()
        {
            var context = new SystemMessageTestContext
            {
                MessageId = Guid.NewGuid().ToString(),
                EnclosedMessageType = null,
                IncludeControlMessageHeader = true,
                ControlMessageHeaderValue = true
            };

            FailedMessageView failure = null;
            Define(context)
                .WithEndpoint<ServerEndpoint>()
                .Done(c => TryGetSingle("/api/errors", out failure, r => r.MessageId == c.MessageId))
                .Run();
            Assert.IsNotNull(failure);
            Assert.IsTrue(failure.IsSystemMessage);
        }

        [Test]
        public void Should_set_the_IsSystemMessage_when_control_message_header_is_null()
        {
            var context = new SystemMessageTestContext
            {
                MessageId = Guid.NewGuid().ToString(),
                EnclosedMessageType = "NServiceBus.Scheduling.Messages.ScheduledTask",
                IncludeControlMessageHeader = true, // If hte control message header is present, then its a system message
                ControlMessageHeaderValue = null
            };

            FailedMessageView failure = null;
            Define(context)
                .WithEndpoint<ServerEndpoint>()
                .Done(c => TryGetSingle("/api/errors", out failure, r => r.MessageId == c.MessageId))
                .Run();
            Assert.IsNotNull(failure);
            Assert.IsTrue(failure.IsSystemMessage);
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

            FailedMessageView failure = null;
            Define(context)
                .WithEndpoint<ServerEndpoint>()
                .Done(c => TryGetSingle("/api/errors", out failure, r => r.MessageId == c.MessageId))
                .Run();
            Assert.IsNotNull(failure);
            Assert.IsFalse(failure.IsSystemMessage);
        }

        public class ServerEndpoint : EndpointConfigurationBuilder
        {
            public ServerEndpoint()
            {
                EndpointSetup<DefaultServerWithAudit>();
            }

            class Foo : IWantToRunWhenBusStartsAndStops
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
                    transportMessage.Headers["NServiceBus.ExceptionInfo.ExceptionType"] = "2014-11-11 02:26:57:767462 Z";
                    transportMessage.Headers["NServiceBus.ExceptionInfo.Message"] = "An error occurred while attempting to extract logical messages from transport message NServiceBus.TransportMessage";
                    transportMessage.Headers["NServiceBus.ExceptionInfo.InnerExceptionType"] = "System.Exception";
                    transportMessage.Headers["NServiceBus.ExceptionInfo.HelpLink"] = String.Empty;
                    transportMessage.Headers["NServiceBus.ExceptionInfo.Source"] = "NServiceBus.Core";
                    transportMessage.Headers["NServiceBus.ExceptionInfo.StackTrace"] = String.Empty;
                    transportMessage.Headers["NServiceBus.FailedQ"] = "SomeEndpoint@SELENE";
                    transportMessage.Headers["NServiceBus.TimeOfFailure"] = "2014-11-11 02:26:58:000462 Z";
                    transportMessage.Headers["NServiceBus.TimeSent"] = "2014-11-11 02:26:01:174786 Z";
                    if (!string.IsNullOrEmpty(SystemMessageTestContext.EnclosedMessageType))
                    {
                        transportMessage.Headers[Headers.EnclosedMessageTypes] = SystemMessageTestContext.EnclosedMessageType;
                    }
                    if (SystemMessageTestContext.IncludeControlMessageHeader)
                    {
                        transportMessage.Headers[Headers.ControlMessageHeader] = SystemMessageTestContext.ControlMessageHeaderValue != null && (bool) SystemMessageTestContext.ControlMessageHeaderValue ? SystemMessageTestContext.ControlMessageHeaderValue.ToString() : null;
                    }

                    SendMessages.Send(transportMessage, new SendOptions(Address.Parse("error")));
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
