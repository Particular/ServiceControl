namespace ServiceControl.AcceptanceTests.Recoverability.MessageRedirects
{
    using System;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.EndpointTemplates;
    using Infrastructure;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Settings;
    using NUnit.Framework;
    using ServiceControl.MessageFailures;
    using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

    class When_a_message_is_retried_with_a_redirect : AcceptanceTest
    {
        [Test]
        [CancelAfter(120_000)]
        public async Task It_should_be_sent_to_the_correct_endpoint(CancellationToken cancellationToken)
        {
            var context = await Define<Context>()
                .WithEndpoint<FromEndpoint>(b => b.When(bus => bus.SendLocal(new MessageToRetry()))
                    .When(async ctx =>
                    {
                        if (ctx.UniqueMessageId == null)
                        {
                            return false;
                        }

                        return await this.TryGet<FailedMessage>($"/api/errors/{ctx.UniqueMessageId}");
                    }, async (bus, ctx) =>
                    {
                        await this.Post("/api/redirects", new RedirectRequest
                        {
                            fromphysicaladdress = ctx.FromAddress,
                            tophysicaladdress = ctx.ToAddress
                        }, status => status != HttpStatusCode.Created);

                        await this.Post<object>($"/api/errors/{ctx.UniqueMessageId}/retry");
                    }).DoNotFailOnErrorMessages())
                .WithEndpoint<ToNewEndpoint>(c =>
                    c.When((session, ctx) =>
                    {
                        ctx.ToAddress = Conventions.EndpointNamingConvention(typeof(ToNewEndpoint));
                        return Task.CompletedTask;
                    }))
                .Done(ctx => ctx.Received)
                .Run(cancellationToken);

            Assert.That(context.Received, Is.True);
        }

        public class FromEndpoint : EndpointConfigurationBuilder
        {
            public FromEndpoint() => EndpointSetup<DefaultServerWithoutAudit>(c => { c.NoRetries(); });

            public class MessageToRetryHandler(
                Context testContext,
                IReadOnlySettings settings,
                ReceiveAddresses receiveAddresses)
                : IHandleMessages<MessageToRetry>
            {
                public Task Handle(MessageToRetry message, IMessageHandlerContext context)
                {
                    testContext.FromAddress = receiveAddresses.MainReceiveAddress;
                    testContext.UniqueMessageId = DeterministicGuid.MakeId(context.MessageId.Replace(@"\", "-"), settings.EndpointName()).ToString();
                    throw new Exception("Message Failed");
                }
            }
        }

        public class ToNewEndpoint : EndpointConfigurationBuilder
        {
            public ToNewEndpoint() => EndpointSetup<DefaultServerWithoutAudit>();

            public class MessageToRetryHandler(Context testContext) : IHandleMessages<MessageToRetry>
            {
                public Task Handle(MessageToRetry message, IMessageHandlerContext context)
                {
                    testContext.Received = true;
                    return Task.CompletedTask;
                }
            }
        }

        public class Context : ScenarioContext
        {
            public string UniqueMessageId { get; set; }
            public string FromAddress { get; set; }
            public string ToAddress { get; set; }
            public bool Received { get; set; }
        }

        public class MessageToRetry : ICommand;
    }
}