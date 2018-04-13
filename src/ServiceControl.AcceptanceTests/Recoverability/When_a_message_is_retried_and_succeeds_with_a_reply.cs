﻿namespace ServiceBus.Management.AcceptanceTests.Recoverability
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Config;
    using NServiceBus.Features;
    using NServiceBus.Settings;
    using NUnit.Framework;
    using ServiceBus.Management.AcceptanceTests.Contexts;
    using ServiceControl.Infrastructure;

    public class When_a_message_is_retried_and_succeeds_with_a_reply : AcceptanceTest
    {
        [Test]
        public async Task The_reply_should_go_to_the_correct_endpoint()
        {
            var context = new RetryReplyContext();

            await Define(context)
                .WithEndpoint<OriginatingEndpoint>(c => c.Given(bus => bus.Send(new OriginalMessage())))
                .WithEndpoint<ReceivingEndpoint>()
                .Done(async c =>
                {
                    if (string.IsNullOrWhiteSpace(c.UniqueMessageId))
                    {
                        return false;
                    }

                    if (!c.RetryIssued)
                    {
                        if (!await TryGet<object>($"/api/errors/{c.UniqueMessageId}"))
                        {
                            return false;
                        }
                        c.RetryIssued = true;
                        await Post<object>($"/api/errors/{c.UniqueMessageId}/retry");
                        return false;
                    }

                    return !string.IsNullOrWhiteSpace(c.ReplyHandledBy);
                })
                .Run(TimeSpan.FromMinutes(3));

            Assert.AreEqual("Originating Endpoint", context.ReplyHandledBy, "Reply handled by incorrect endpoint");
        }

        class OriginalMessage : IMessage { }

        class ReplyMessage : IMessage { }

        class RetryReplyContext : ScenarioContext
        {
            public bool RetryIssued { get; set; }
            public string UniqueMessageId { get; set; }
            public string ReplyHandledBy { get; set; }
        }

        class OriginatingEndpoint : EndpointConfigurationBuilder
        {
            public OriginatingEndpoint()
            {
                EndpointSetup<DefaultServerWithoutAudit>()
                    .AddMapping<OriginalMessage>(typeof(ReceivingEndpoint));
            }

            public class ReplyMessageHandler : IHandleMessages<ReplyMessage>
            {
                public RetryReplyContext Context { get; set; }

                public void Handle(ReplyMessage message)
                {
                    Context.ReplyHandledBy = "Originating Endpoint";
                }
            }
        }

        class ReceivingEndpoint : EndpointConfigurationBuilder
        {
            public ReceivingEndpoint()
            {
                EndpointSetup<DefaultServerWithoutAudit>(c => c.DisableFeature<SecondLevelRetries>())
                    .WithConfig<TransportConfig>(c =>
                    {
                        c.MaxRetries = 0;
                    });
            }

            public class OriginalMessageHandler : IHandleMessages<OriginalMessage>
            {
                public IBus Bus { get; set; }
                public RetryReplyContext Context { get; set; }
                public ReadOnlySettings Settings { get; set; }

                public void Handle(OriginalMessage message)
                {
                    var messageId = Bus.CurrentMessageContext.Id.Replace(@"\", "-");
                    Context.UniqueMessageId = DeterministicGuid.MakeId(messageId, Settings.LocalAddress().Queue).ToString();

                    if (!Context.RetryIssued)
                    {
                        throw new Exception("This is still the original attempt");
                    }
                    Bus.Reply(new ReplyMessage());
                }
            }

            public class ReplyMessageHandler : IHandleMessages<ReplyMessage>
            {
                public RetryReplyContext Context { get; set; }
                public void Handle(ReplyMessage message)
                {
                    Context.ReplyHandledBy = "Receiving Endpoint";
                }
            }
        }
    }
}
