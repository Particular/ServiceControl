namespace ServiceControl.AcceptanceTests.Recoverability.MessageFailures
{
    using System;
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
    using TestSupport;

    class When_a_retry_for_a_failed_message_fails : AcceptanceTest
    {
        [Test]
        [CancelAfter(120_000)]
        public async Task It_should_be_marked_as_unresolved(CancellationToken cancellationToken)
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
                .Run(cancellationToken);

            Assert.That(result.Result.Status, Is.EqualTo(FailedMessageStatus.Unresolved));
        }

        [Test]
        [CancelAfter(120_000)]
        public async Task It_should_be_able_to_be_retried_successfully(CancellationToken cancellationToken)
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
                .Do("DetectThirdFailure", async ctx => await CheckProcessingAttemptsIs(ctx, 3))
                .Do("RetryThirdTime", async ctx =>
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
                .Run(cancellationToken);

            Assert.That(result.Result.Status, Is.EqualTo(FailedMessageStatus.Resolved));
        }

        Task<SingleResult<FailedMessage>> CheckProcessingAttemptsIs(MyContext ctx, int count) => GetFailedMessage(ctx, f => f.ProcessingAttempts.Count == count && f.Status == FailedMessageStatus.Unresolved);

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
            public FailureEndpoint() =>
                EndpointSetup<DefaultServerWithoutAudit>(c =>
                {
                    c.NoRetries();
                    c.ReportSuccessfulRetriesToServiceControl();
                });

            public class MyMessageHandler(MyContext scenarioContext, IReadOnlySettings settings)
                : IHandleMessages<MyMessage>
            {
                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    Console.WriteLine("Attempting to process message");

                    scenarioContext.EndpointNameOfReceivingEndpoint = settings.EndpointName();
                    scenarioContext.MessageId = context.MessageId.Replace(@"\", "-");

                    if (!scenarioContext.Succeed) //simulate that the exception will be resolved with the retry
                    {
                        Console.WriteLine("Message processing failure");
                        throw new Exception("Simulated exception");
                    }

                    Console.WriteLine("Message processing success");
                    return Task.CompletedTask;
                }
            }
        }


        public class MyMessage : ICommand;

        public class MyContext : ScenarioContext, ISequenceContext
        {
            public string MessageId { get; set; }

            public string EndpointNameOfReceivingEndpoint { get; set; }

            public bool Succeed { get; set; }

            public string UniqueMessageId => DeterministicGuid.MakeId(MessageId, EndpointNameOfReceivingEndpoint).ToString();

            public FailedMessage Result { get; set; }
            public int Step { get; set; }
        }
    }
}