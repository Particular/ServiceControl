namespace ServiceBus.Management.AcceptanceTests.Recoverability
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using EndpointTemplates;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.MessageMutator;
    using NServiceBus.Routing;
    using NServiceBus.Transport;
    using NUnit.Framework;
    using ServiceControl.MessageFailures.Api;
    using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

    public class When_a_message_is_retried_with_a_replyTo_header : AcceptanceTest
    {
        [Test]
        public async Task The_header_should_not_be_changed()
        {
            var context = await Define<ReplyToContext>(ctx => { ctx.ReplyToAddress = "ReplyToAddress@SOMEMACHINE"; })
                .WithEndpoint<VerifyHeaderEndpoint>()
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

            Assert.AreEqual(context.ReplyToAddress, context.ReceivedReplyToAddress);
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

        class VerifyHeaderEndpoint : EndpointConfigurationBuilder
        {
            public VerifyHeaderEndpoint()
            {
                EndpointSetup<DefaultServerWithoutAudit>(
                    (c, r) => c.RegisterMessageMutator(new VerifyHeaderIsUnchanged((ReplyToContext)r.ScenarioContext))
                );
            }

            class FakeSender : DispatchRawMessages<ReplyToContext>
            {
                protected override TransportOperations CreateMessage(ReplyToContext context)
                {
                    var messageId = Guid.NewGuid().ToString();
                    var headers = new Dictionary<string, string>
                    {
                        [Headers.MessageId] = messageId,
                        [Headers.ReplyToAddress] = context.ReplyToAddress,
                        [Headers.ProcessingEndpoint] = Conventions.EndpointNamingConvention(typeof(VerifyHeaderEndpoint)),
                        ["NServiceBus.ExceptionInfo.ExceptionType"] = typeof(Exception).FullName,
                        ["NServiceBus.ExceptionInfo.Message"] = "Bad thing happened",
                        ["NServiceBus.ExceptionInfo.InnerExceptionType"] = "System.Exception",
                        ["NServiceBus.ExceptionInfo.Source"] = "NServiceBus.Core",
                        ["NServiceBus.ExceptionInfo.StackTrace"] = String.Empty,
                        ["NServiceBus.FailedQ"] = Conventions.EndpointNamingConvention(typeof(VerifyHeaderEndpoint)),
                        ["NServiceBus.TimeOfFailure"] = "2014-11-11 02:26:58:000462 Z",
                        [Headers.EnclosedMessageTypes] = typeof(OriginalMessage).AssemblyQualifiedName,
                        [Headers.MessageIntent] = MessageIntentEnum.Send.ToString()
                    };

                    var outgoingMessage = new OutgoingMessage(messageId, headers, new byte[0]);

                    return new TransportOperations(new TransportOperation(outgoingMessage, new UnicastAddressTag("error")));
                }
            }

            class VerifyHeaderIsUnchanged : IMutateIncomingTransportMessages
            {
                public VerifyHeaderIsUnchanged(ReplyToContext context)
                {
                    replyToContext = context;
                }

                public Task MutateIncoming(MutateIncomingTransportMessageContext context)
                {
                    if (context.Headers.TryGetValue(Headers.ReplyToAddress, out var replyToAddress))
                    {
                        replyToContext.ReceivedReplyToAddress = replyToAddress;
                    }

                    replyToContext.Done = true;
                    return Task.FromResult(0);
                }

                ReplyToContext replyToContext;
            }
        }
    }
}