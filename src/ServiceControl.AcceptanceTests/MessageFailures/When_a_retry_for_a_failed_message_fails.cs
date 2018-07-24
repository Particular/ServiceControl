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
            var result = await Define<MyContext>(ctx => { ctx.Succeed = false; })
                .WithEndpoint<FailureEndpoint>(b =>
                    b.When(bus => bus.SendLocal(new MyMessage()))
                        .DoNotFailOnErrorMessages()
                )
                .Do("DetectFirstFailure", async ctx => await CheckProcessingAttemptsIs(ctx, 1))
                .Do("RetryFirstTime", async ctx =>
                {
                    await this.Post<object>($"/api/errors/{ctx.UniqueMessageId}/retry");
                    return true;
                })
                .Do("DetectSecondFailure", async ctx => await CheckProcessingAttemptsIs(ctx, 2))
                .Do("RetrySecondTime", async ctx =>
                {
                    await this.Post<object>($"/api/errors/{ctx.UniqueMessageId}/retry");
                    return true;
                })
                .Do("DetectThirdFailure", async ctx =>
                {
                    ctx.Result = await this.TryGet<FailedMessage>($"/api/errors/{ctx.UniqueMessageId}");
                    return ctx.Result.ProcessingAttempts.Count == 3;
                })
                .Done()
                .Run(TimeSpan.FromMinutes(2));

            Assert.AreEqual(FailedMessageStatus.Unresolved, result.Result.Status);
        }

        [Test]
        public async Task It_should_be_able_to_be_retried_successfully()
        {
            var result = await Define<MyContext>(ctx => { ctx.Succeed = false; })
                .WithEndpoint<FailureEndpoint>(b =>
                    b.When(bus => bus.SendLocal(new MyMessage()))
                        .DoNotFailOnErrorMessages()
                )
                .Do("DetectFirstFailure", async ctx => await CheckProcessingAttemptsIs(ctx, 1))
                .Do("RetryFirstTime", async ctx =>
                {
                    await this.Post<object>($"/api/errors/{ctx.UniqueMessageId}/retry");
                    return true;
                })
                .Do("DetectSecondFailure", async ctx => await CheckProcessingAttemptsIs(ctx, 2))
                .Do("RetrySecondTime", async ctx =>
                {
                    await this.Post<object>($"/api/errors/{ctx.UniqueMessageId}/retry");
                    return true;
                })
                .Do("DetectThirdFailure", async ctx => await CheckProcessingAttemptsIs(ctx, 2))
                .Do("RetryFourthTime", async ctx =>
                {
                    ctx.Succeed = true;
                    await this.Post<object>($"/api/errors/{ctx.UniqueMessageId}/retry");
                    return true;
                })
                .Do("DetectSuccess", async ctx =>
                {
                    ctx.Result = await GetFailedMessage(ctx, f => f.Status == FailedMessageStatus.Resolved);
                    return ctx.Result != null;
                })
                .Done()
                .Run(TimeSpan.FromMinutes(2));

            Assert.AreEqual(FailedMessageStatus.Resolved, result.Result.Status);
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

        public class MyContext : ScenarioContext, ISequenceContext
        {
            public string MessageId { get; set; }

            public string EndpointNameOfReceivingEndpoint { get; set; }

            public bool Succeed { get; set; }

            public string UniqueMessageId => DeterministicGuid.MakeId(MessageId, EndpointNameOfReceivingEndpoint).ToString();
            public int Step { get; set; }

            public FailedMessage Result { get; set; }
        }
    }
}