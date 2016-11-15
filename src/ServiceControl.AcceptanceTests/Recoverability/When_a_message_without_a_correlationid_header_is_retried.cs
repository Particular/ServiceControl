namespace ServiceBus.Management.AcceptanceTests.Recoverability
{
    using System;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Config;
    using NServiceBus.Features;
    using NServiceBus.MessageMutator;
    using NServiceBus.Unicast.Messages;
    using NUnit.Framework;
    using Contexts;
    using NServiceBus.Settings;
    using ServiceControl.Infrastructure;

    public class When_a_message_without_a_correlationid_header_is_retried : AcceptanceTest
    {
        [Test]
        public void The_successful_retry_should_succeed()
        {
            var context = Define<MyContext>()
                .WithEndpoint<Receiver>(b => b.Given(bus => bus.SendLocal(new MyMessage())))
                .Done(ctx =>
                {
                    if (string.IsNullOrWhiteSpace(ctx.UniqueMessageId))
                    {
                        return false;
                    }

                    if (!ctx.RetryIssued)
                    {
                        object _;
                        if (!TryGet($"/api/errors/{ctx.UniqueMessageId}", out _))
                        {
                            return false;
                        }

                        ctx.RetryIssued = true;
                        Post<object>($"/api/errors/{ctx.UniqueMessageId}/retry");
                        return false;
                    }

                    return ctx.RetryHandled;
                })
                .Run();

            Assert.IsTrue(context.RetryHandled, "Retry not handled correctly");
        }

        class MyMessage : IMessage { }

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
                    c.DisableFeature<SecondLevelRetries>();
                    c.RegisterComponents(components => components.ConfigureComponent<CorrelationIdRemover>(DependencyLifecycle.InstancePerCall));
                })
                .WithConfig<TransportConfig>(c =>
                {
                    c.MaxRetries = 0;
                });
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public IBus Bus { get; set; }

                public MyContext TestContext { get; set; }
                public ReadOnlySettings Settings { get; set; }

                public void Handle(MyMessage message)
                {
                    var messageId = Bus.CurrentMessageContext.Id.Replace(@"\", "-");

                    TestContext.UniqueMessageId = DeterministicGuid.MakeId(messageId, Settings.LocalAddress().ToString()).ToString();

                    if (!TestContext.RetryIssued)
                    {
                        throw new Exception("Simulated exception");
                    }

                    TestContext.RetryHandled = true;
                }
            }

            class CorrelationIdRemover : IMutateOutgoingTransportMessages
            {
                public void MutateOutgoing(LogicalMessage logicalMessage, TransportMessage transportMessage)
                {
                    var hasCorrelationId = transportMessage.Headers.ContainsKey(Headers.CorrelationId);

                    if (!hasCorrelationId)
                    {
                        return;
                    }

                    transportMessage.CorrelationId = null;
                    transportMessage.Headers.Remove(Headers.CorrelationId);
                }
            }
        }
    }
}