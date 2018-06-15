﻿

namespace ServiceBus.Management.AcceptanceTests.MessageFailures
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Settings;
    using NUnit.Framework;
    using ServiceBus.Management.AcceptanceTests.Contexts;
    using ServiceControl.Infrastructure;

    public class When_a_invalid_id_is_sent_to_retry : AcceptanceTest
    {
        [Test]
        public async Task SubsequentBatchesShouldBeProcessed()
        {
            var context = new MyContext();

            await Define(context)
                .WithEndpoint<FailureEndpoint>(cfg => cfg
                    .When(async bus =>
                    {
                        while (true)
                        {
                            try
                            {
                                await Post<object>("/api/errors/1785201b-5ccd-4705-b14e-f9dd7ef1386e/retry");
                                break;
                            }
                            catch (InvalidOperationException)
                            {
                                // api not up yet
                            }
                        }

                        await bus.SendLocal(new MessageThatWillFail());
                    })
                    .When(async ctx => ctx.IssueRetry && await TryGet<object>("/api/errors/" + ctx.UniqueMessageId), (bus, ctx) => Post<object>($"/api/errors/{ctx.UniqueMessageId}/retry")))
                .Done(ctx => ctx.Done)
                .Run(TimeSpan.FromMinutes(3));

            Assert.IsTrue(context.Done);
        }

        public class FailureEndpoint : EndpointConfigurationBuilder
        {
            public FailureEndpoint()
            {
                EndpointSetup<DefaultServerWithoutAudit>(c =>
                {
                    var recoverability = c.Recoverability();
                    recoverability.Immediate(s => s.NumberOfRetries(0));
                    recoverability.Delayed(s => s.NumberOfRetries(0));
                });
            }

            public class MessageThatWillFailHandler: IHandleMessages<MessageThatWillFail>
            {
                public MyContext Context { get; set; }
                public ReadOnlySettings Settings { get; set; }

                public Task Handle(MessageThatWillFail message, IMessageHandlerContext context)
                {
                    if (!Context.ExceptionThrown) //simulate that the exception will be resolved with the retry
                    {
                        Context.UniqueMessageId = DeterministicGuid.MakeId(context.MessageId.Replace(@"\", "-"), Settings.LocalAddress()).ToString();
                        Context.ExceptionThrown = Context.IssueRetry = true;
                        throw new Exception("Simulated exception");
                    }

                    Context.Done = true;
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

        [Serializable]
        public class MessageThatWillFail : ICommand
        {
        }
    }
}
