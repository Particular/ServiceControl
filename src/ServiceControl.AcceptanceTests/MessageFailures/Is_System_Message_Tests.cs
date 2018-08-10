namespace ServiceBus.Management.AcceptanceTests.MessageFailures
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using EndpointTemplates;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Routing;
    using NServiceBus.Transport;
    using NUnit.Framework;
    using ServiceControl.MessageFailures.Api;

    class Is_System_Message_Tests : AcceptanceTest
    {
        [Test]
        public async Task Should_set_the_IsSystemMessage_when_message_type_is_not_a_scheduled_task()
        {
            FailedMessageView failure = null;
            await Define<SystemMessageTestContext>(ctx =>
                {
                    ctx.MessageId = Guid.NewGuid().ToString();
                    ctx.EnclosedMessageType = "SendOnlyError.SendSomeCommand, TestSendOnlyError, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null";
                    ctx.IncludeControlMessageHeader = false;
                })
                .WithEndpoint<ServerEndpoint>()
                .Done(async c =>
                {
                    var result = await this.TryGetSingle<FailedMessageView>("/errors", r => r.MessageId == c.MessageId);
                    failure = result;
                    return result;
                })
                .Run();

            Assert.IsNotNull(failure);
            Assert.IsFalse(failure.IsSystemMessage);
        }

        [Test]
        public async Task Should_set_the_IsSystemMessage_when_message_type_is_a_scheduled_task()
        {
            FailedMessageView failure = null;
            await Define<SystemMessageTestContext>(ctx =>
                {
                    ctx.MessageId = Guid.NewGuid().ToString();
                    ctx.EnclosedMessageType = "NServiceBus.Scheduling.Messages.ScheduledTask";
                    ctx.IncludeControlMessageHeader = false;
                })
                .WithEndpoint<ServerEndpoint>()
                .Done(async c =>
                {
                    var result = await this.TryGetSingle<FailedMessageView>("/errors", r => r.MessageId == c.MessageId);
                    failure = result;
                    return result;
                })
                .Run();
            Assert.IsNotNull(failure);
            Assert.IsTrue(failure.IsSystemMessage);
        }

        [Test]
        public async Task Should_set_the_IsSystemMessage_when_control_message_header_is_true()
        {
            FailedMessageView failure = null;
            await Define<SystemMessageTestContext>(ctx =>
                {
                    ctx.MessageId = Guid.NewGuid().ToString();
                    ctx.EnclosedMessageType = null;
                    ctx.IncludeControlMessageHeader = true;
                    ctx.ControlMessageHeaderValue = true;
                })
                .WithEndpoint<ServerEndpoint>()
                .Done(async c =>
                {
                    var result = await this.TryGetSingle<FailedMessageView>("/errors", r => r.MessageId == c.MessageId);
                    failure = result;
                    return result;
                })
                .Run();
            Assert.IsNotNull(failure);
            Assert.IsTrue(failure.IsSystemMessage);
        }

        [Test]
        public async Task Should_set_the_IsSystemMessage_when_control_message_header_is_null()
        {
            FailedMessageView failure = null;
            await Define<SystemMessageTestContext>(ctx =>
                {
                    ctx.MessageId = Guid.NewGuid().ToString();
                    ctx.EnclosedMessageType = "NServiceBus.Scheduling.Messages.ScheduledTask";
                    ctx.IncludeControlMessageHeader = true; // If hte control message header is present, then its a system message
                    ctx.ControlMessageHeaderValue = null;
                })
                .WithEndpoint<ServerEndpoint>()
                .Done(async c =>
                {
                    var result = await this.TryGetSingle<FailedMessageView>("/errors", r => r.MessageId == c.MessageId);
                    failure = result;
                    return result;
                })
                .Run();

            Assert.IsNotNull(failure);
            Assert.IsTrue(failure.IsSystemMessage);
        }

        [Test]
        public async Task Should_set_the_IsSystemMessage_for_integration_scenario()
        {
            FailedMessageView failure = null;
            await Define<SystemMessageTestContext>(ctx =>
                {
                    ctx.MessageId = Guid.NewGuid().ToString();
                    ctx.EnclosedMessageType = null;
                    ctx.IncludeControlMessageHeader = false;
                })
                .WithEndpoint<ServerEndpoint>()
                .Done(async c =>
                {
                    var result = await this.TryGetSingle<FailedMessageView>("/errors", r => r.MessageId == c.MessageId);
                    failure = result;
                    return result;
                })
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

            class Foo : DispatchRawMessages<SystemMessageTestContext>
            {
                protected override TransportOperations CreateMessage(SystemMessageTestContext context)
                {
                    // Transport message has no headers for Processing endpoint and the ReplyToAddress is set to null
                    var headers = new Dictionary<string, string>
                    {
                        [Headers.ProcessingEndpoint] = "ServerEndpoint",
                        [Headers.MessageId] = context.MessageId,
                        [Headers.ConversationId] = "a59395ee-ec80-41a2-a728-a3df012fc707",
                        ["$.diagnostics.hostid"] = "bdd4b0510bff5a6d07e91baa7e16a804",
                        ["$.diagnostics.hostdisplayname"] = "SELENE",
                        ["NServiceBus.ExceptionInfo.ExceptionType"] = "2014-11-11 02:26:57:767462 Z",
                        ["NServiceBus.ExceptionInfo.Message"] = "An error occurred while attempting to extract logical messages from transport message NServiceBus.TransportMessage",
                        ["NServiceBus.ExceptionInfo.InnerExceptionType"] = "System.Exception",
                        ["NServiceBus.ExceptionInfo.HelpLink"] = String.Empty,
                        ["NServiceBus.ExceptionInfo.Source"] = "NServiceBus.Core",
                        ["NServiceBus.ExceptionInfo.StackTrace"] = String.Empty,
                        ["NServiceBus.FailedQ"] = "SomeEndpoint@SELENE",
                        ["NServiceBus.TimeOfFailure"] = "2014-11-11 02:26:58:000462 Z",
                        ["NServiceBus.TimeSent"] = "2014-11-11 02:26:01:174786 Z"
                    };
                    if (!string.IsNullOrEmpty(context.EnclosedMessageType))
                    {
                        headers[Headers.EnclosedMessageTypes] = context.EnclosedMessageType;
                    }

                    if (context.IncludeControlMessageHeader)
                    {
                        headers[Headers.ControlMessageHeader] = context.ControlMessageHeaderValue != null && (bool)context.ControlMessageHeaderValue ? context.ControlMessageHeaderValue.ToString() : null;
                    }

                    return new TransportOperations(new TransportOperation(new OutgoingMessage(context.MessageId, headers, new byte[0]), new UnicastAddressTag("error")));
                }
            }
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