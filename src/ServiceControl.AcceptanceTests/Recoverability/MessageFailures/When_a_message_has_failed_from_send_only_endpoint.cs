﻿namespace ServiceControl.AcceptanceTests.Recoverability.MessageFailures
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.EndpointTemplates;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Routing;
    using NServiceBus.Transport;
    using NUnit.Framework;
    using ServiceControl.MessageFailures.Api;

    class When_a_message_has_failed_from_send_only_endpoint : AcceptanceTest
    {
        [Test]
        public async Task Should_be_listed_in_the_error_list_when_processing_endpoint_header_is_not_present()
        {
            FailedMessageView failure = null;
            await Define<MyContext>(ctx =>
                {
                    ctx.MessageId = Guid.NewGuid().ToString();
                    ctx.IncludeProcessingEndpointHeader = false;
                })
                .WithEndpoint<SendOnlyEndpoint>()
                .Done(async c =>
                {
                    var result = await this.TryGetSingle<FailedMessageView>("/api/errors", r => r.MessageId == c.MessageId);
                    failure = result;
                    return result;
                })
                .Run();
            Assert.That(failure, Is.Not.Null);
            Assert.That(failure.ReceivingEndpoint.Name, Does.Contain("SomeEndpoint"), $"The sending endpoint should be SomeEndpoint and not {failure.ReceivingEndpoint.Name}");
        }

        [Test]
        public async Task Should_be_listed_in_the_error_list_when_processing_endpoint_header_is_present()
        {
            FailedMessageView failure = null;
            await Define<MyContext>(ctx =>
                {
                    ctx.MessageId = Guid.NewGuid().ToString();
                    ctx.IncludeProcessingEndpointHeader = true;
                })
                .WithEndpoint<SendOnlyEndpoint>()
                .Done(async c =>
                {
                    var result = await this.TryGetSingle<FailedMessageView>("/api/errors", r => r.MessageId == c.MessageId);
                    failure = result;
                    return result;
                })
                .Run();
            Assert.That(failure, Is.Not.Null);
            Assert.That(failure.ReceivingEndpoint.Name, Does.Contain("SomeEndpoint"), $"The sending endpoint should be SomeEndpoint and not {failure.ReceivingEndpoint.Name}");
        }

        public class SendOnlyEndpoint : EndpointConfigurationBuilder
        {
            public SendOnlyEndpoint() => EndpointSetup<DefaultServerWithoutAudit>();

            class Foo : DispatchRawMessages<MyContext>
            {
                protected override TransportOperations CreateMessage(MyContext context)
                {
                    // Transport message has no headers for Processing endpoint and the ReplyToAddress is set to null
                    var headers = new Dictionary<string, string>
                    {
                        [Headers.MessageId] = context.MessageId,
                        [Headers.ConversationId] = "a59395ee-ec80-41a2-a728-a3df012fc707",
                        ["$.diagnostics.hostid"] = "bdd4b0510bff5a6d07e91baa7e16a804",
                        ["$.diagnostics.hostdisplayname"] = "SELENE",
                        ["NServiceBus.ExceptionInfo.ExceptionType"] = "2014-11-11 02:26:57:767462 Z",
                        ["NServiceBus.ExceptionInfo.Message"] = "An error occurred while attempting to extract logical messages from transport message NServiceBus.TransportMessage",
                        ["NServiceBus.ExceptionInfo.InnerExceptionType"] = "System.Exception",
                        ["NServiceBus.ExceptionInfo.HelpLink"] = string.Empty,
                        ["NServiceBus.ExceptionInfo.Source"] = "NServiceBus.Core",
                        ["NServiceBus.ExceptionInfo.StackTrace"] = string.Empty,
                        ["NServiceBus.FailedQ"] = "SomeEndpoint@SELENE",
                        ["NServiceBus.TimeOfFailure"] = "2014-11-11 02:26:58:000462 Z",
                        ["NServiceBus.TimeSent"] = "2014-11-11 02:26:01:174786 Z",
                        [Headers.EnclosedMessageTypes] = "SendOnlyError.SendSomeCommand, TestSendOnlyError, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null"
                    };

                    if (context.IncludeProcessingEndpointHeader)
                    {
                        headers[Headers.ProcessingEndpoint] = "SomeEndpoint";
                    }

                    var outgoingMessage = new OutgoingMessage(context.MessageId, headers, new byte[0]);

                    return new TransportOperations(
                        new TransportOperation(outgoingMessage, new UnicastAddressTag("error"))
                    );
                }
            }
        }

        public class MyMessage : IMessage;

        public class MyContext : ScenarioContext
        {
            public string MessageId { get; set; }
            public bool IncludeProcessingEndpointHeader { get; set; }
        }
    }
}