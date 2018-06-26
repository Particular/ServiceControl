namespace ServiceBus.Management.AcceptanceTests.MessageRedirects
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Settings;
    using NUnit.Framework;
    using ServiceBus.Management.AcceptanceTests.EndpointTemplates;
    using ServiceControl.Infrastructure;
    using ServiceControl.MessageFailures;
    using ServiceControl.MessageFailures.Api;
    using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

    class When_a_message_fails_a_retry_with_a_redirect : AcceptanceTest
    {
        [Test]
        public async Task The_original_failed_message_record_is_updated()
        {
            List<FailedMessageView> failedMessages = null;

            await Define<Context>()
                .WithEndpoint<OriginalEndpoint>(b =>
                        b.When(bus => bus.SendLocal(new MessageToRetry()))
                            .When( // Failed Message Received
                                async ctx => ctx.UniqueMessageId != null && await this.TryGet<FailedMessage>($"/api/errors/{ctx.UniqueMessageId}"),
                                async (bus, ctx) =>
                                {
                                    // Create Redirect
                                    await this.Post("/api/redirects", new RedirectRequest
                                    {
                                        fromphysicaladdress = ctx.FromAddress,
                                        tophysicaladdress = ctx.ToAddress
                                    }, status => status != HttpStatusCode.Created);

                                    // Retry Failed Message
                                    await this.Post<object>($"/api/errors/{ctx.UniqueMessageId}/retry");
                                }).DoNotFailOnErrorMessages()
                )
                .WithEndpoint<NewEndpoint>(c =>
                {
                    c.When((session, ctx) =>
                    {
                        ctx.ToAddress = Conventions.EndpointNamingConvention(typeof(NewEndpoint));
                        return Task.FromResult(0);
                    }).DoNotFailOnErrorMessages();
                })
                .Done(async ctx =>
                {
                    var result = await this.TryGetMany<FailedMessageView>("/api/errors", msg => msg.Exception.Message.Contains("Message Failed In New Endpoint Too"));
                    failedMessages = result;
                    return ctx.ProcessedAgain&& result;
                })
                .Run();

            Assert.IsNotNull(failedMessages);
            Assert.IsNotEmpty(failedMessages);
            Assert.AreEqual(1, failedMessages.Count);

            var failedMessageView = failedMessages.Single();
            Assert.AreEqual(2, failedMessageView.NumberOfProcessingAttempts);
            Assert.AreEqual(FailedMessageStatus.Unresolved, failedMessageView.Status);
        }

        class OriginalEndpoint : EndpointConfigurationBuilder
        {
            public OriginalEndpoint()
            {
                EndpointSetup<DefaultServerWithoutAudit>(c =>
                    {
                        c.NoRetries();
                    });
            }

            public class MessageToRetryHandler : IHandleMessages<MessageToRetry>
            {
                public Context Context { get; set; }
                public ReadOnlySettings Settings { get; set; }

                public Task Handle(MessageToRetry message, IMessageHandlerContext context)
                {
                    Context.FromAddress = Settings.LocalAddress();
                    Context.UniqueMessageId = DeterministicGuid.MakeId(context.MessageId.Replace(@"\", "-"), Settings.EndpointName()).ToString();
                    throw new Exception("Message Failed");
                }
            }
        }

        class NewEndpoint : EndpointConfigurationBuilder
        {
            public NewEndpoint()
            {
                EndpointSetup<DefaultServerWithoutAudit>(c =>
                    {
                        c.NoRetries();
                    });
            }

            public class MessageToRetryHandler : IHandleMessages<MessageToRetry>
            {
                public Context Context { get; set; }

                public Task Handle(MessageToRetry message, IMessageHandlerContext context)
                {
                    Context.ProcessedAgain = true;
                    throw new Exception("Message Failed In New Endpoint Too");
                }
            }
        }

        class Context : ScenarioContext
        {
            public string FromAddress { get; set; }
            public string ToAddress { get; set; }
            public string UniqueMessageId { get; set; }
            public bool ProcessedAgain { get; set; }
        }

        class MessageToRetry : ICommand
        {

        }
    }
}