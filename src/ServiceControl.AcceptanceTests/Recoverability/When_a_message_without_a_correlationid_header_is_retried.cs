namespace ServiceControl.AcceptanceTests.Recoverability
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.EndpointTemplates;
    using Infrastructure;
    using Microsoft.Extensions.DependencyInjection;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.MessageMutator;
    using NServiceBus.Settings;
    using NUnit.Framework;


    class When_a_message_without_a_correlationid_header_is_retried : AcceptanceTest
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

            Assert.That(context.RetryHandled, Is.True, "Retry not handled correctly");
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
            public Receiver() =>
                EndpointSetup<DefaultServerWithoutAudit>(c =>
                {
                    c.NoRetries();
                    c.RegisterComponents(services => services.AddSingleton<CorrelationIdRemover>());
                });

            public class MyMessageHandler(MyContext testContext, IReadOnlySettings settings)
                : IHandleMessages<MyMessage>
            {
                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    var messageId = context.MessageId.Replace(@"\", "-");

                    testContext.UniqueMessageId = DeterministicGuid.MakeId(messageId, settings.EndpointName()).ToString();

                    if (!testContext.RetryIssued)
                    {
                        throw new Exception("Simulated exception");
                    }

                    testContext.RetryHandled = true;
                    return Task.CompletedTask;
                }
            }

            class CorrelationIdRemover : IMutateOutgoingTransportMessages
            {
                public Task MutateOutgoing(MutateOutgoingTransportMessageContext context)
                {
                    context.OutgoingHeaders.Remove(Headers.CorrelationId);
                    return Task.CompletedTask;
                }
            }
        }
    }
}