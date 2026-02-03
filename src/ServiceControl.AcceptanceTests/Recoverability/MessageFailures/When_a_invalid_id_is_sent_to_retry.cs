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

    class When_a_invalid_id_is_sent_to_retry : AcceptanceTest
    {
        [Test]
        [CancelAfter(180_000)]
        public async Task SubsequentBatchesShouldBeProcessed(CancellationToken cancellationToken)
        {
            var context = await Define<MyContext>()
                .WithEndpoint<FailureEndpoint>(cfg => cfg
                    .When(async bus =>
                    {
                        while (true)
                        {
                            try
                            {
                                await this.Post<object>("/api/errors/1785201b-5ccd-4705-b14e-f9dd7ef1386e/retry");
                                break;
                            }
                            catch (InvalidOperationException)
                            {
                                // api not up yet
                            }
                        }

                        await bus.SendLocal(new MessageThatWillFail());
                    }).DoNotFailOnErrorMessages()
                    .When(async ctx => ctx.IssueRetry && await this.TryGet<object>("/api/errors/" + ctx.UniqueMessageId), (bus, ctx) => this.Post<object>($"/api/errors/{ctx.UniqueMessageId}/retry")).DoNotFailOnErrorMessages())
                .Done(ctx => ctx.Done)
                .Run(cancellationToken);

            Assert.That(context.Done, Is.True);
        }

        public class FailureEndpoint : EndpointConfigurationBuilder
        {
            public FailureEndpoint() =>
                EndpointSetup<DefaultServerWithoutAudit>(c =>
                {
                    c.NoRetries();
                });

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

        public class MyContext : ScenarioContext
        {
            public bool Done { get; set; }
            public bool ExceptionThrown { get; set; }
            public bool IssueRetry { get; set; }
            public string UniqueMessageId { get; set; }
        }


        public class MessageThatWillFail : ICommand
        {
        }
    }
}