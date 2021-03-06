﻿namespace ServiceControl.AcceptanceTests.Recoverability.MessageFailures
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Infrastructure;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Settings;
    using NUnit.Framework;
    using TestSupport.EndpointTemplates;

    class When_a_invalid_id_is_sent_to_retry : AcceptanceTest
    {
        [Test]
        public async Task SubsequentBatchesShouldBeProcessed()
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
                .Run(TimeSpan.FromMinutes(3));

            Assert.IsTrue(context.Done);
        }

        public class FailureEndpoint : EndpointConfigurationBuilder
        {
            public FailureEndpoint()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    var recoverability = c.Recoverability();
                    recoverability.Immediate(s => s.NumberOfRetries(0));
                    recoverability.Delayed(s => s.NumberOfRetries(0));
                });
            }

            public class MessageThatWillFailHandler : IHandleMessages<MessageThatWillFail>
            {
                readonly MyContext scenarioContext;
                readonly ReadOnlySettings settings;

                public MessageThatWillFailHandler(MyContext scenarioContext, ReadOnlySettings settings)
                {
                    this.scenarioContext = scenarioContext;
                    this.settings = settings;
                }

                public Task Handle(MessageThatWillFail message, IMessageHandlerContext context)
                {
                    if (!scenarioContext.ExceptionThrown) //simulate that the exception will be resolved with the retry
                    {
                        scenarioContext.UniqueMessageId = DeterministicGuid.MakeId(context.MessageId, settings.EndpointName()).ToString();
                        scenarioContext.ExceptionThrown = scenarioContext.IssueRetry = true;
                        throw new Exception("Simulated exception");
                    }

                    scenarioContext.Done = true;
                    return Task.FromResult(0);
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