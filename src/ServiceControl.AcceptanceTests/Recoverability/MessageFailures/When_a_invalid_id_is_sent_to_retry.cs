namespace ServiceControl.AcceptanceTests.Recoverability.MessageFailures
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

    class When_a_invalid_id_is_sent_to_retry : AcceptanceTest
    {
        [Test]
        [CancelAfter(180_000)]
        public async Task SubsequentBatchesShouldBeProcessed(CancellationToken cancellationToken = default)
        {
            HttpStatusCode retryOfUnknownId = default;

            await Define<MyContext>()
                .WithEndpoint<FailureEndpoint>(cfg => cfg
                    .When(async bus =>
                    {
                        using var response = await HttpClient.PostAsync($"/api/errors/{UnknownFailedMessageId}/retry", null, cancellationToken);

                        retryOfUnknownId = response.StatusCode;

                        await bus.SendLocal(new MessageThatWillFail());
                    }).DoNotFailOnErrorMessages())
                .Do("Wait for the message to fail", async ctx =>
                    ctx.IssueRetry && await this.TryGet<object>($"/api/errors/{ctx.UniqueMessageId}"))
                .Do("Retry the failed message", async ctx =>
                    await this.Post<object>($"/api/errors/{ctx.UniqueMessageId}/retry"))
                .Do("Wait for the retry to be handled", ctx => Task.FromResult(ctx.Done))
                .Done()
                .Run(cancellationToken);

            Assert.That(retryOfUnknownId, Is.EqualTo(HttpStatusCode.Accepted));
        }

        public class FailureEndpoint : EndpointConfigurationBuilder
        {
            public FailureEndpoint() =>
                EndpointSetup<DefaultServerWithoutAudit>(c =>
                {
                    c.NoRetries();
                });

            [Handler]
            public class MessageThatWillFailHandler(MyContext scenarioContext, IReadOnlySettings settings)
                : IHandleMessages<MessageThatWillFail>
            {
                public Task Handle(MessageThatWillFail message, IMessageHandlerContext context)
                {
                    if (!scenarioContext.ExceptionThrown) //simulate that the exception will be resolved with the retry
                    {
                        scenarioContext.UniqueMessageId = DeterministicGuid.MakeId(context.MessageId, settings.EndpointName()).ToString();
                        scenarioContext.ExceptionThrown = scenarioContext.IssueRetry = true;
                        throw new Exception("Simulated exception");
                    }

                    scenarioContext.Done = true;
                    return Task.CompletedTask;
                }
            }
        }

        public class MyContext : ScenarioContext, ISequenceContext
        {
            public int Step { get; set; }
            public bool Done { get; set; }
            public bool ExceptionThrown { get; set; }
            public bool IssueRetry { get; set; }
            public string UniqueMessageId { get; set; }
        }


        const string UnknownFailedMessageId = "1785201b-5ccd-4705-b14e-f9dd7ef1386e";

        public class MessageThatWillFail : ICommand;
    }
}