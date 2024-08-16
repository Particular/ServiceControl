namespace ServiceControl.AcceptanceTests.Recoverability.MessageRedirects
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.EndpointTemplates;
    using Infrastructure;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Settings;
    using NUnit.Framework;
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
                        return Task.CompletedTask;
                    }).DoNotFailOnErrorMessages();
                })
                .Done(async ctx =>
                {
                    var result = await this.TryGetMany<FailedMessageView>("/api/errors", msg => msg.Exception.Message.Contains("Message Failed In New Endpoint Too"));
                    failedMessages = result;
                    return ctx.ProcessedAgain && result;
                })
                .Run();

            Assert.That(failedMessages, Is.Not.Null);
            Assert.IsNotEmpty(failedMessages);
            Assert.That(failedMessages.Count, Is.EqualTo(1));

            var failedMessageView = failedMessages.Single();
            Assert.That(failedMessageView.NumberOfProcessingAttempts, Is.EqualTo(2));
            Assert.That(failedMessageView.Status, Is.EqualTo(FailedMessageStatus.Unresolved));
        }

        class OriginalEndpoint : EndpointConfigurationBuilder
        {
            public OriginalEndpoint() => EndpointSetup<DefaultServerWithoutAudit>(c => c.NoRetries());

            public class MessageToRetryHandler(
                Context scenarioContext,
                IReadOnlySettings settings,
                ReceiveAddresses receiveAddresses)
                : IHandleMessages<MessageToRetry>
            {
                public Task Handle(MessageToRetry message, IMessageHandlerContext context)
                {
                    scenarioContext.FromAddress = receiveAddresses.MainReceiveAddress;
                    scenarioContext.UniqueMessageId = DeterministicGuid.MakeId(context.MessageId.Replace(@"\", "-"), settings.EndpointName()).ToString();
                    throw new Exception("Message Failed");
                }
            }
        }

        class NewEndpoint : EndpointConfigurationBuilder
        {
            public NewEndpoint() => EndpointSetup<DefaultServerWithoutAudit>(c => { c.NoRetries(); });

            public class MessageToRetryHandler(Context testContext) : IHandleMessages<MessageToRetry>
            {
                public Task Handle(MessageToRetry message, IMessageHandlerContext context)
                {
                    testContext.ProcessedAgain = true;
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

        class MessageToRetry : ICommand;
    }
}