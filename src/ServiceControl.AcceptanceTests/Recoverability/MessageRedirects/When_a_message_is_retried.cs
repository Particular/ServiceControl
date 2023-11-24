namespace ServiceControl.AcceptanceTests.Recoverability.MessageRedirects
{
    using System;
    using System.Net;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Infrastructure;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Settings;
    using NUnit.Framework;
    using ServiceControl.MessageFailures;
    using TestSupport.EndpointTemplates;
    using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

    class When_a_message_is_retried_with_a_redirect : AcceptanceTest
    {
        [Test]
        public async Task It_should_be_sent_to_the_correct_endpoint()
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
                .Run(TimeSpan.FromSeconds(120));

            Assert.IsTrue(context.Received);
        }

        public class FromEndpoint : EndpointConfigurationBuilder
        {
            public FromEndpoint()
            {
                EndpointSetup<DefaultServer>(c => { c.NoRetries(); });
            }

            public class MessageToRetryHandler : IHandleMessages<MessageToRetry>
            {
                readonly Context scenarioContext;
                readonly ReceiveAddresses receiveAddresses;
                readonly IReadOnlySettings settings;

                public MessageToRetryHandler(Context scenarioContext, IReadOnlySettings settings, ReceiveAddresses receiveAddresses)
                {
                    this.scenarioContext = scenarioContext;
                    this.receiveAddresses = receiveAddresses;
                    this.settings = settings;
                }

                public Task Handle(MessageToRetry message, IMessageHandlerContext context)
                {
                    scenarioContext.FromAddress = receiveAddresses.MainReceiveAddress;
                    scenarioContext.UniqueMessageId = DeterministicGuid.MakeId(context.MessageId.Replace(@"\", "-"), settings.EndpointName()).ToString();
                    throw new Exception("Message Failed");
                }
            }
        }

        public class ToNewEndpoint : EndpointConfigurationBuilder
        {
            public ToNewEndpoint()
            {
                EndpointSetup<DefaultServer>();
            }

            public class MessageToRetryHandler : IHandleMessages<MessageToRetry>
            {
                readonly Context scenarioContext;

                public MessageToRetryHandler(Context scenarioContext)
                {
                    this.scenarioContext = scenarioContext;
                }

                public Task Handle(MessageToRetry message, IMessageHandlerContext context)
                {
                    scenarioContext.Received = true;
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

        public class MessageToRetry : ICommand
        {
        }
    }
}