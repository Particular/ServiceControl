namespace ServiceBus.Management.AcceptanceTests.MessageRedirects
{
    using System;
    using System.Net;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Config;
    using NServiceBus.Features;
    using NServiceBus.Settings;
    using NUnit.Framework;
    using ServiceBus.Management.AcceptanceTests.Contexts;
    using ServiceControl.Infrastructure;
    using ServiceControl.MessageFailures;

    class When_a_message_is_retried_with_a_redirect : AcceptanceTest
    {
        [Test]
        public void It_should_be_sent_to_the_correct_endpoint()
        {
            FailedMessage failedMessage;

            var context = Define<Context>()
                .WithEndpoint<FromEndpoint>(b => b.Given(bus =>
                {
                    bus.SendLocal(new MessageToRetry());
                }).When(ctx =>
                {
                    if (ctx.UniqueMessageId == null)
                    {
                        return false;
                    }

                    return TryGet($"/api/errors/{ctx.UniqueMessageId}", out failedMessage);
                }, (bus, ctx) =>
                {
                    Post("/api/redirects", new RedirectRequest
                    {
                        fromphysicaladdress = ctx.FromAddress,
                        tophysicaladdress = ctx.ToAddress
                    }, status => status != HttpStatusCode.Created);

                    Post<object>($"/api/errors/{ctx.UniqueMessageId}/retry");
                }))
                .WithEndpoint<ToNewEndpoint>()
                .Done(ctx => ctx.Received)
                .Run(TimeSpan.FromSeconds(120));

            Assert.IsTrue(context.Received);
        }

        public class FromEndpoint : EndpointConfigurationBuilder
        {
            public FromEndpoint()
            {
                EndpointSetup<DefaultServerWithoutAudit>(c => c.DisableFeature<SecondLevelRetries>())
                    .WithConfig<TransportConfig>(c =>
                    {
                        c.MaxRetries = 1;
                    });
            }

            public class MessageToRetryHandler : IHandleMessages<MessageToRetry>
            {
                public Context Context { get; set; }
                public IBus Bus { get; set; }
                public ReadOnlySettings Settings { get; set; }

                public void Handle(MessageToRetry message)
                {
                    Context.FromAddress = Settings.LocalAddress().ToString();
                    Context.UniqueMessageId = DeterministicGuid.MakeId(Bus.CurrentMessageContext.Id.Replace(@"\", "-"), Settings.LocalAddress().Queue).ToString();
                    throw new Exception("Message Failed");
                }
            }
        }

        public class ToNewEndpoint : EndpointConfigurationBuilder
        {
            public ToNewEndpoint()
            {
                EndpointSetup<DefaultServerWithoutAudit>(c => c.EndpointName("mynewendpoint"));
            }

            public class EndpointDiscovery : IWantToRunWhenBusStartsAndStops
            {
                public Context Context { get; set; }
                public ReadOnlySettings Settings { get; set; }

                public void Start()
                {
                    Context.ToAddress = Settings.LocalAddress().ToString();
                }

                public void Stop()
                {
                }
            }

            public class MessageToRetryHandler : IHandleMessages<MessageToRetry>
            {
                public Context Context { get; set; }

                public void Handle(MessageToRetry message)
                {
                    Context.Received = true;
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
