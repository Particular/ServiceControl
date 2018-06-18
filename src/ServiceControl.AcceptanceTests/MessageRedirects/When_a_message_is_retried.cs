﻿namespace ServiceBus.Management.AcceptanceTests.MessageRedirects
{
    using System;
    using System.Net;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Configuration.AdvancedExtensibility;
    using NServiceBus.Settings;
    using NUnit.Framework;
    using ServiceBus.Management.AcceptanceTests.Contexts;
    using ServiceControl.Infrastructure;
    using ServiceControl.MessageFailures;
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

                    return await TryGet<FailedMessage>($"/api/errors/{ctx.UniqueMessageId}");
                }, async (bus, ctx) =>
                {
                    await Post("/api/redirects", new RedirectRequest
                    {
                        fromphysicaladdress = ctx.FromAddress,
                        tophysicaladdress = ctx.ToAddress
                    }, status => status != HttpStatusCode.Created);

                    await Post<object>($"/api/errors/{ctx.UniqueMessageId}/retry");
                }))
                .WithEndpoint<ToNewEndpoint>(c => 
                    c.When((session, ctx) =>
                    {
                        ctx.ToAddress = Conventions.EndpointNamingConvention(typeof(ToNewEndpoint));
                        return Task.FromResult(0);
                    }))
                .Done(ctx => ctx.Received)
                .Run(TimeSpan.FromSeconds(120));

            Assert.IsTrue(context.Received);
        }

        public class FromEndpoint : EndpointConfigurationBuilder
        {
            public FromEndpoint()
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
                    Context.UniqueMessageId = DeterministicGuid.MakeId(context.MessageId.Replace(@"\", "-"), Settings.LocalAddress()).ToString();
                    throw new Exception("Message Failed");
                }
            }
        }

        public class ToNewEndpoint : EndpointConfigurationBuilder
        {
            public ToNewEndpoint()
            {
                EndpointSetup<DefaultServerWithoutAudit>(c => c.GetSettings().Set("NServiceBus.Routing.EndpointName", "mynewendpoint"));
            }

            public class MessageToRetryHandler : IHandleMessages<MessageToRetry>
            {
                public Context Context { get; set; }

                public Task Handle(MessageToRetry message, IMessageHandlerContext context)
                {
                    Context.Received = true;
                    return Task.FromResult(0);
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

        [Serializable]
        public class MessageToRetry : ICommand
        {
        }
    }
}
