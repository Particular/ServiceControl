namespace ServiceBus.Management.AcceptanceTests.Recoverability
{
    using System;
    using System.Threading.Tasks;
    using EndpointTemplates;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.MessageMutator;
    using NServiceBus.Settings;
    using NUnit.Framework;
    using ServiceControl.Infrastructure;

    public class When_a_message_without_a_correlationid_header_is_retried : AcceptanceTest
    {
        [Test]
        public async Task The_successful_retry_should_succeed()
        {
            var context = await Define<MyContext>()
                .WithEndpoint<Receiver>(b => b.When(bus => bus.SendLocal(new MyMessage()))
                    .DoNotFailOnErrorMessages())
                .Done(async ctx =>
                {
                    if (string.IsNullOrWhiteSpace(ctx.UniqueMessageId))
                    {
                        return false;
                    }

                    if (!ctx.RetryIssued)
                    {
                        if (!await this.TryGet<object>($"/api/errors/{ctx.UniqueMessageId}"))
                        {
                            return false;
                        }

                        ctx.RetryIssued = true;
                        await this.Post<object>($"/api/errors/{ctx.UniqueMessageId}/retry");
                        return false;
                    }

                    return ctx.RetryHandled;
                })
                .Run();

            Assert.IsTrue(context.RetryHandled, "Retry not handled correctly");
        }

        class MyMessage : IMessage
        {
        }

        class MyContext : ScenarioContext
        {
            public string UniqueMessageId { get; set; }
            public bool RetryIssued { get; set; }
            public bool RetryHandled { get; set; }
        }

        class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultServerWithoutAudit>(c =>
                {
                    c.NoRetries();
                    c.RegisterComponents(components => components.ConfigureComponent<CorrelationIdRemover>(DependencyLifecycle.InstancePerCall));
                });
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public MyContext TestContext { get; set; }
                public ReadOnlySettings Settings { get; set; }

                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    var messageId = context.MessageId.Replace(@"\", "-");

                    // TODO: Check LocalAddress should just be queue name
                    TestContext.UniqueMessageId = DeterministicGuid.MakeId(messageId, Settings.EndpointName()).ToString();

                    if (!TestContext.RetryIssued)
                    {
                        throw new Exception("Simulated exception");
                    }

                    TestContext.RetryHandled = true;
                    return Task.FromResult(0);
                }
            }

            class CorrelationIdRemover : IMutateOutgoingTransportMessages
            {
                public Task MutateOutgoing(MutateOutgoingTransportMessageContext context)
                {
                    context.OutgoingHeaders.Remove(Headers.CorrelationId);
                    return Task.FromResult(0);
                }
            }
        }
    }
}