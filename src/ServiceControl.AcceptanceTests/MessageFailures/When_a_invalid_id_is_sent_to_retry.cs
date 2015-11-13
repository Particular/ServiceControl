

namespace ServiceBus.Management.AcceptanceTests.MessageFailures
{
    using System;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Config;
    using NServiceBus.Features;
    using NServiceBus.Settings;
    using NUnit.Framework;
    using ServiceBus.Management.AcceptanceTests.Contexts;
    using ServiceControl.Infrastructure;

    [Serializable]
    public class When_a_invalid_id_is_sent_to_retry : AcceptanceTest
    {
        [Test]
        public void SubsequentBatchesShouldBeProcessed()
        {
            var context = new MyContext();

            Scenario.Define(context)
                .WithEndpoint<ManagementEndpoint>(ctx => ctx.AppConfig(PathToAppConfig))
                .WithEndpoint<FailureEndpoint>(ctx => ctx
                    .When(bus =>
                    {
                        while (true)
                        {
                            try
                            {
                                Post<object>("/api/errors/1785201b-5ccd-4705-b14e-f9dd7ef1386e/retry");
                                break;
                            }
                            catch (InvalidOperationException)
                            {
                                // api not up yet
                            }
                        }
                        
                        bus.SendLocal(new MessageThatWillFail());
                    }))
                .Done(ctx =>
                {
                    if (ctx.IssueRetry)
                    {
                        object failure;
                        if (!TryGet("/api/errors/" + ctx.UniqueMessageId, out failure))
                        {
                            return false;
                        }

                        ctx.IssueRetry = false;
                        Post<object>(String.Format("/api/errors/{0}/retry", ctx.UniqueMessageId));
                    }

                    return ctx.Done;
                })
                .Run(TimeSpan.FromMinutes(3));

            Assert.IsTrue(context.Done);
        }

        public class FailureEndpoint : EndpointConfigurationBuilder
        {
            public FailureEndpoint()
            {
                EndpointSetup<DefaultServer>(c => c.DisableFeature<SecondLevelRetries>())
                    .WithConfig<TransportConfig>(c =>
                    {
                        c.MaxRetries = 1;
                    })
                    .AuditTo(Address.Parse("audit"));
            }

            public class MessageThatWillFailHandler: IHandleMessages<MessageThatWillFail>
            {
                public MyContext Context { get; set; }
                public IBus Bus { get; set; }
                public ReadOnlySettings Settings { get; set; }

                public void Handle(MessageThatWillFail message)
                {
                    if (!Context.ExceptionThrown) //simulate that the exception will be resolved with the retry
                    {
                        Context.UniqueMessageId = DeterministicGuid.MakeId(Bus.CurrentMessageContext.Id.Replace(@"\", "-"), Settings.EndpointName()).ToString();
                        Context.ExceptionThrown = Context.IssueRetry = true;
                        throw new Exception("Simulated exception");
                    }

                    Context.Done = true;
                }
            }
        }

        [Serializable]
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
