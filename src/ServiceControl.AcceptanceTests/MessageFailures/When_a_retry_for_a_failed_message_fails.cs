namespace ServiceBus.Management.AcceptanceTests.MessageFailures
{
    using System;
    using System.Threading.Tasks;
    using EndpointTemplates;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Settings;
    using NUnit.Framework;
    using ServiceControl.Infrastructure;
    using ServiceControl.MessageFailures;

    public class When_a_retry_for_a_failed_message_fails : AcceptanceTest
    {
        [Test]
        public async Task It_should_be_marked_as_unresolved()
        {
            FailedMessage failure = null;

            await Define<MyContext>(ctx => { ctx.Succeed = false; })
                .WithEndpoint<FailureEndpoint>(b =>
                    b.When(bus => bus.SendLocal(new MyMessage()))
                        .When(async ctx => await CheckProcessingAttemptsIs(ctx, 1),
                            (bus, ctx) => this.Post<object>($"/api/errors/{ctx.UniqueMessageId}/retry"))
                        .When(async ctx => await CheckProcessingAttemptsIs(ctx, 2),
                            (bus, ctx) => this.Post<object>($"/api/errors/{ctx.UniqueMessageId}/retry"))
                        .DoNotFailOnErrorMessages()
                )
                .Done(async ctx =>
                {
                    var result = await GetFailedMessage(ctx, f => f.ProcessingAttempts.Count == 3);
                    failure = result;
                    return result;
                })
                .Run(TimeSpan.FromMinutes(1));

            Assert.IsNotNull(failure, "Failure should not be null");
            Assert.AreEqual(FailedMessageStatus.Unresolved, failure.Status);
        }

        [Test]
        public async Task It_should_be_able_to_be_retried_successfully()
        {
            FailedMessage failure = null;

            await Define<MyContext>(ctx => { ctx.Succeed = false; })
                .WithEndpoint<FailureEndpoint>(b =>
                    b.When(bus => bus.SendLocal(new MyMessage()))
                        .When(async ctx => await CheckProcessingAttemptsIs(ctx, 1),
                            (bus, ctx) => this.Post<object>($"/api/errors/{ctx.UniqueMessageId}/retry"))
                        .When(async ctx => await CheckProcessingAttemptsIs(ctx, 2),
                            (bus, ctx) => this.Post<object>($"/api/errors/{ctx.UniqueMessageId}/retry"))
                        .When(async ctx => await CheckProcessingAttemptsIs(ctx, 3),
                            async (bus, ctx) =>
                            {
                                ctx.Succeed = true;
                                await this.Post<object>($"/api/errors/{ctx.UniqueMessageId}/retry");
                            })
                        .DoNotFailOnErrorMessages()
                )
                .Done(async ctx =>
                {
                    var result = await GetFailedMessage(ctx, f => f.Status == FailedMessageStatus.Resolved);
                    failure = result;
                    return result;
                })
                .Run(TimeSpan.FromMinutes(1));

            Assert.IsNotNull(failure, "Failure should not be null");
            Assert.AreEqual(FailedMessageStatus.Resolved, failure.Status);
        }

        Task<SingleResult<FailedMessage>> CheckProcessingAttemptsIs(MyContext ctx, int count)
        {
            return GetFailedMessage(ctx, f => f.ProcessingAttempts.Count == count);
        }

        async Task<SingleResult<FailedMessage>> GetFailedMessage(MyContext c, Predicate<FailedMessage> condition)
        {
            if (c.UniqueMessageId == null)
            {
                return SingleResult<FailedMessage>.Empty;
            }

            return await this.TryGet($"/api/errors/{c.UniqueMessageId}", condition);
        }

        public class FailureEndpoint : EndpointConfigurationBuilder
        {
            public FailureEndpoint()
            {
                EndpointSetup<DefaultServerWithAudit>(c => { c.NoRetries(); });
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public MyContext Context { get; set; }
                public ReadOnlySettings Settings { get; set; }

                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    Console.WriteLine("Attempting to process message");

                    Context.EndpointNameOfReceivingEndpoint = Settings.EndpointName();
                    Context.MessageId = context.MessageId.Replace(@"\", "-");

                    if (!Context.Succeed) //simulate that the exception will be resolved with the retry
                    {
                        Console.WriteLine("Message processing failure");
                        throw new Exception("Simulated exception");
                    }

                    Console.WriteLine("Message processing success");
                    return Task.FromResult(0);
                }
            }
        }


        public class MyMessage : ICommand
        {
        }

        public class MyContext : ScenarioContext
        {
            public string MessageId { get; set; }

            public string EndpointNameOfReceivingEndpoint { get; set; }

            public bool Succeed { get; set; }

            public string UniqueMessageId => DeterministicGuid.MakeId(MessageId, EndpointNameOfReceivingEndpoint).ToString();
        }
    }
}