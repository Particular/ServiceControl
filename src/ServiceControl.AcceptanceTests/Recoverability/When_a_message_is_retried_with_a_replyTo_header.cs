namespace ServiceControl.AcceptanceTests.Recoverability
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.EndpointTemplates;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.MessageMutator;
    using NServiceBus.Routing;
    using NServiceBus.Transport;
    using NUnit.Framework;
    using ServiceControl.MessageFailures.Api;
    using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;


    class When_a_message_is_retried_with_a_replyTo_header : AcceptanceTest
    {
        [Test]
        public async Task The_header_should_not_be_changed()
        {
            var context = await Define<ReplyToContext>(ctx => { ctx.ReplyToAddress = "ReplyToAddress@SOMEMACHINE"; })
                .WithEndpoint<VerifyHeader>()
                .Done(async x =>
                {
                    if (!x.RetryIssued && await this.TryGetMany<FailedMessageView>("/api/errors"))
                    {
                        x.RetryIssued = true;
                        await this.Post<object>("/api/errors/retry/all");
                    }

                    return x.Done;
                })
                .Run();

            Assert.That(context.ReceivedReplyToAddress, Is.EqualTo(context.ReplyToAddress));
        }

        class OriginalMessage : IMessage
        {
        }

        class ReplyToContext : ScenarioContext
        {
            public string ReplyToAddress { get; set; }
            public string ReceivedReplyToAddress { get; set; }
            public bool RetryIssued { get; set; }
            public bool Done { get; set; }
        }

        class VerifyHeader : EndpointConfigurationBuilder
        {
            public VerifyHeader() =>
                EndpointSetup<DefaultServerWithoutAudit>(
                    (c, r) =>
                    {
                        c.EnableFeature<FakeSender>();
                        c.RegisterMessageMutator(new VerifyHeaderIsUnchanged((ReplyToContext)r.ScenarioContext));
                    });

            class FakeSender : DispatchRawMessages<ReplyToContext>
            {
                protected override TransportOperations CreateMessage(ReplyToContext context)
                {
                    var messageId = Guid.NewGuid().ToString();
                    var headers = new Dictionary<string, string>
                    {
                        [Headers.MessageId] = messageId,
                        [Headers.ReplyToAddress] = context.ReplyToAddress,
                        [Headers.ProcessingEndpoint] = Conventions.EndpointNamingConvention(typeof(VerifyHeader)),
                        ["NServiceBus.ExceptionInfo.ExceptionType"] = typeof(Exception).FullName,
                        ["NServiceBus.ExceptionInfo.Message"] = "Bad thing happened",
                        ["NServiceBus.ExceptionInfo.InnerExceptionType"] = "System.Exception",
                        ["NServiceBus.ExceptionInfo.Source"] = "NServiceBus.Core",
                        ["NServiceBus.ExceptionInfo.StackTrace"] = string.Empty,
                        ["NServiceBus.FailedQ"] = Conventions.EndpointNamingConvention(typeof(VerifyHeader)),
                        ["NServiceBus.TimeOfFailure"] = "2014-11-11 02:26:58:000462 Z",
                        [Headers.EnclosedMessageTypes] = typeof(OriginalMessage).AssemblyQualifiedName,
                        [Headers.MessageIntent] = MessageIntent.Send.ToString()
                    };

                    var outgoingMessage = new OutgoingMessage(messageId, headers, new byte[0]);

                    return new TransportOperations(new TransportOperation(outgoingMessage, new UnicastAddressTag("error")));
                }
            }

            class VerifyHeaderIsUnchanged : IMutateIncomingTransportMessages
            {
                public VerifyHeaderIsUnchanged(ReplyToContext context) => testContext = context;

                public Task MutateIncoming(MutateIncomingTransportMessageContext context)
                {
                    if (context.Headers.TryGetValue(Headers.ReplyToAddress, out var replyToAddress))
                    {
                        testContext.ReceivedReplyToAddress = replyToAddress;
                    }

                    testContext.Done = true;
                    return Task.CompletedTask;
                }

                readonly ReplyToContext testContext;
            }
        }
    }
}