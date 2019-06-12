﻿namespace ServiceControl.AcceptanceTests.Recoverability
{
    using System;
    using System.Threading.Tasks;
    using Infrastructure;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTesting.Customization;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.Settings;
    using NUnit.Framework;
    using ServiceBus.Management.AcceptanceTests;
    using ServiceBus.Management.AcceptanceTests.EndpointTemplates;

    [RunOnAllTransports]
    class When_a_message_is_retried_and_succeeds_with_a_reply : AcceptanceTest
    {
        [Test]
        public async Task The_reply_should_go_to_the_correct_endpoint()
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
                .Run(TimeSpan.FromMinutes(1));

            Assert.AreEqual("Originating Endpoint", context.ReplyHandledBy, "Reply handled by incorrect endpoint");
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
            public Originator()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    var routing = c.ConfigureTransport().Routing();
                    routing.RouteToEndpoint(typeof(OriginalMessage), typeof(Receiver));
                });
            }

            public class ReplyMessageHandler : IHandleMessages<ReplyMessage>
            {
                public RetryReplyContext Context { get; set; }

                public Task Handle(ReplyMessage message, IMessageHandlerContext context)
                {
                    Context.ReplyHandledBy = "Originating Endpoint";
                    return Task.FromResult(0);
                }
            }
        }

        class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultServer>(c => { c.NoRetries(); });
            }

            public class OriginalMessageHandler : IHandleMessages<OriginalMessage>
            {
                public RetryReplyContext Context { get; set; }
                public ReadOnlySettings Settings { get; set; }

                public Task Handle(OriginalMessage message, IMessageHandlerContext context)
                {
                    var messageId = context.MessageId.Replace(@"\", "-");
                    // TODO: Check if the local address needs to be sanitized
                    Context.UniqueMessageId = DeterministicGuid.MakeId(messageId, Settings.EndpointName()).ToString();

                    if (!Context.RetryIssued)
                    {
                        throw new Exception("This is still the original attempt");
                    }

                    return context.Reply(new ReplyMessage());
                }
            }

            public class ReplyMessageHandler : IHandleMessages<ReplyMessage>
            {
                public RetryReplyContext Context { get; set; }

                public Task Handle(ReplyMessage message, IMessageHandlerContext context)
                {
                    Context.ReplyHandledBy = "Receiving Endpoint";
                    return Task.FromResult(0);
                }
            }
        }
    }
}