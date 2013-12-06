namespace ServiceBus.Management.AcceptanceTests
{
    using System;
    using System.Linq;
    using System.Threading;
    using Contexts;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Config;
    using NServiceBus.Features;
    using NUnit.Framework;
    using ServiceControl.Contracts.Operations;
    using ServiceControl.MessageAuditing;

    public class When_issuing_a_retry_for_a_failed_message : AcceptanceTest
    {
        public class FailureEndpoint : EndpointConfigurationBuilder
        {
            public FailureEndpoint()
            {
                EndpointSetup<DefaultServer>(c => Configure.Features.Disable<SecondLevelRetries>())
                    .WithConfig<TransportConfig>(c =>
                        {
                            c.MaxRetries = 1;
                        })
                    .AddMapping<MyMessage>(typeof(FailureEndpoint))
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
            public AuditMessage Message { get; set; }

            public string EndpointNameOfReceivingEndpoint { get; set; }

            public bool RetryIssued { get; set; }
        }

        bool AuditDataAvailable (MyContext context, MessageStatus status)
        {
            if (context.MessageId == null)
            {
                return false;
            }

            context.Message =
                Get<AuditMessage>(String.Format("/api/messages/{0}-{1}", context.MessageId,
                    context.EndpointNameOfReceivingEndpoint));

            if (context.Message == null)
            {
                return false;
            }

            return context.Message.Status == status;
        }

        [Test]
        public void Should_be_imported_and_accessible_via_the_rest_api()
        {
            var context = new MyContext();

            Scenario.Define(context)
                .WithEndpoint<ManagementEndpoint>(c => c.AppConfig(PathToAppConfig))
                .WithEndpoint<FailureEndpoint>(b => b.Given(bus => bus.Send(new MyMessage())))
                .Done(c =>
                {
                    lock (context)
                    {
                        var b = c.RetryIssued;
                        if (!b && AuditDataAvailable(c, MessageStatus.Failed))
                        {
                            c.RetryIssued = true;

                            Post<object>(String.Format("/api/errors/{0}/retry", c.Message.Id));

                            return false;
                        }

                        var auditDataAvailable = AuditDataAvailable(c, MessageStatus.Successful);

                        if (!auditDataAvailable)
                        {
                            Thread.Sleep(2000);
                        }

                        return auditDataAvailable;
                    }
                })
                .Run(TimeSpan.FromMinutes(2));

            Assert.IsNotNull(context.Message.ProcessedAt,
                "Processed at should be set when the message has been successfully been processed");
            var historyItems = context.Message.History.ToList();
            Assert.AreEqual(historyItems.OrderBy(h => h.Time).First().Action, "RetryIssued",
                "There should be an audit trail for retry attempts");
            Assert.AreEqual(historyItems.OrderBy(h => h.Time).Skip(1).First().Action, "ErrorResolved",
                "There should be an audit trail for successful retries");
        }
    }
}