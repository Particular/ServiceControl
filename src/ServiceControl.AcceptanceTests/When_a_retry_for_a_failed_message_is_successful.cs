namespace ServiceBus.Management.AcceptanceTests
{
    using System;
    using System.Threading;
    using Contexts;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Config;
    using NServiceBus.Features;
    using NUnit.Framework;
    using ServiceControl.Contracts.Operations;
    using ServiceControl.MessageFailures;

    public class When_a_retry_for_a_failed_message_is_successful : AcceptanceTest
    {
        [Test]
        public void Should_show_up_in_the_list_of_successfuly_processed_messages()
        {
            FailedMessage failure = null;

            var context = new MyContext();

            Scenario.Define(context)
                .WithEndpoint<ManagementEndpoint>(c => c.AppConfig(PathToAppConfig))
                .WithEndpoint<FailureEndpoint>(b => b.Given(bus => bus.SendLocal(new MyMessage())))
                .Done(c =>
                {
                  
                    var failedMessageId = string.Format("{0}-{1}", c.MessageId,
                        c.EndpointNameOfReceivingEndpoint);


                    if (!TryGet("/api/errors/" + failedMessageId, out failure))
                    {
                        return false;
                    }

                    Console.Out.WriteLine("Status: " + failure.Status);

                    if (c.RetryIssued)
                    {
                        Assert.AreNotEqual(MessageStatus.RepeatedFailure,failure.Status);

                        Thread.Sleep(1000); //todo: add support for a "default" delay when Done() returns false
                        return failure.Status == MessageStatus.Successful;
                    }
                    else
                    {
                        c.RetryIssued = true;

                        Post<object>(String.Format("/api/errors/{0}/retry", failedMessageId));

                        return false;
                    }
                })
                .Run(TimeSpan.FromMinutes(2));

            Assert.AreEqual(MessageStatus.Successful,failure.Status);
            //todo check the audit component instead
            //var historyItems = context.Message.History.ToList();
            //Assert.AreEqual(historyItems.OrderBy(h => h.Time).First().Action, "RetryIssued",
            //    "There should be an audit trail for retry attempts");
            //Assert.AreEqual(historyItems.OrderBy(h => h.Time).Skip(1).First().Action, "ErrorResolved",
            //    "There should be an audit trail for successful retries");
        }


        public class FailureEndpoint : EndpointConfigurationBuilder
        {
            public FailureEndpoint()
            {
                EndpointSetup<DefaultServer>(c => Configure.Features.Disable<SecondLevelRetries>())
                    .WithConfig<TransportConfig>(c =>
                        {
                            c.MaxRetries = 1;
                        })
                    .AuditTo(Address.Parse("audit"));
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public MyContext Context { get; set; }

                public IBus Bus { get; set; }

                public void Handle(MyMessage message)
                {
                    Context.EndpointNameOfReceivingEndpoint = Configure.EndpointName;
                    Context.MessageId = Bus.CurrentMessageContext.Id.Replace(@"\","-");

                    if (!Context.RetryIssued) //simulate that the exception will be resolved with the retry
                    {
                        throw new Exception("Simulated exception");
                    }
                }
            }
        }

        [Serializable]
        public class MyMessage : ICommand
        {
        }

        public class MyContext : ScenarioContext
        {
            public string MessageId { get; set; }
            public FailedMessage Message { get; set; }

            public string EndpointNameOfReceivingEndpoint { get; set; }

            public bool RetryIssued { get; set; }
        }

        bool FailedMessageFound (MyContext context)
        {
            if (context.MessageId == null)
            {
                return false;
            }

            context.Message =
                Get<FailedMessage>(String.Format("/api/errors/{0}-{1}", context.MessageId,
                    context.EndpointNameOfReceivingEndpoint));

            if (context.Message == null)
            {
                return false;
            }

            return true;
        }

 
    }
}