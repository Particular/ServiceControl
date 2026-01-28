namespace ServiceControl.AcceptanceTests.Recoverability
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.EndpointTemplates;
    using Infrastructure;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTesting.Customization;
    using NServiceBus.Settings;
    using NUnit.Framework;


    class When_a_message_is_retried_and_succeeds_with_a_reply : AcceptanceTest
    {
        [Test]
        [CancelAfter(60_000)]
        public async Task The_reply_should_go_to_the_correct_endpoint(CancellationToken cancellation)
        {
            var context = await Define<RetryReplyContext>()
                .WithEndpoint<Originator>(c => c.When(bus => bus.Send(new OriginalMessage())))
                .WithEndpoint<Receiver>(c => c.DoNotFailOnErrorMessages())
                .Done(async c =>
                {
                    if (string.IsNullOrWhiteSpace(c.UniqueMessageId))
                    {
                        return false;
                    }

                    if (!c.RetryIssued)
                    {
                        if (!await this.TryGet<object>($"/api/errors/{c.UniqueMessageId}"))
                        {
                            return false;
                        }

                        c.RetryIssued = true;
                        await this.Post<object>($"/api/errors/{c.UniqueMessageId}/retry");
                        return false;
                    }

                    return !string.IsNullOrWhiteSpace(c.ReplyHandledBy);
                })
                .Run(cancellation);

            Assert.That(context.ReplyHandledBy, Is.EqualTo("Originating Endpoint"), "Reply handled by incorrect endpoint");
        }

        class OriginalMessage : IMessage
        {
        }

        class ReplyMessage : IMessage
        {
        }

        class RetryReplyContext : ScenarioContext
        {
            public bool RetryIssued { get; set; }
            public string UniqueMessageId { get; set; }
            public string ReplyHandledBy { get; set; }
        }

        class Originator : EndpointConfigurationBuilder
        {
            public Originator() => EndpointSetup<DefaultServerWithoutAudit>(c =>
            {
                var routing = c.ConfigureRouting();
                routing.RouteToEndpoint(typeof(OriginalMessage), typeof(Receiver));
            });

            public class ReplyMessageHandler(RetryReplyContext testContext) : IHandleMessages<ReplyMessage>
            {
                public Task Handle(ReplyMessage message, IMessageHandlerContext context)
                {
                    testContext.ReplyHandledBy = "Originating Endpoint";
                    return Task.CompletedTask;
                }
            }
        }

        class Receiver : EndpointConfigurationBuilder
        {
            public Receiver() => EndpointSetup<DefaultServerWithoutAudit>(c => c.NoRetries());

            public class OriginalMessageHandler(RetryReplyContext testContext, IReadOnlySettings settings)
                : IHandleMessages<OriginalMessage>
            {
                public Task Handle(OriginalMessage message, IMessageHandlerContext context)
                {
                    var messageId = context.MessageId.Replace(@"\", "-");

                    testContext.UniqueMessageId = DeterministicGuid.MakeId(messageId, settings.EndpointName()).ToString();

                    if (!testContext.RetryIssued)
                    {
                        throw new Exception("This is still the original attempt");
                    }

                    return context.Reply(new ReplyMessage());
                }
            }

            public class ReplyMessageHandler(RetryReplyContext testContext) : IHandleMessages<ReplyMessage>
            {
                public Task Handle(ReplyMessage message, IMessageHandlerContext context)
                {
                    testContext.ReplyHandledBy = "Receiving Endpoint";
                    return Task.CompletedTask;
                }
            }
        }
    }
}