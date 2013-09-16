namespace ServiceControl.AcceptanceTests
{
    using System;
    using System.Linq;
    using Contexts;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Features;
    using NUnit.Framework;
    using Alerts;
    using MessageAuditing;

    public class When_a_message_has_failed : AcceptanceTest
    {
        public class Sender : EndpointConfigurationBuilder
        {
            public Sender()
            {
                EndpointSetup<DefaultServer>()
                    .AddMapping<MyMessage>(typeof(Receiver));
            }
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultServer>(c => Configure.Features.Disable<SecondLevelRetries>())
                    .AuditTo(Address.Parse("audit"));
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public MyContext Context { get; set; }

                public IBus Bus { get; set; }

                public void Handle(MyMessage message)
                {
                    Context.EndpointNameOfReceivingEndpoint = Configure.EndpointName;
                    Context.MessageId = Bus.CurrentMessageContext.Id;
                    throw new Exception("Simulated exception");
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
            public Message Message { get; set; }
            public Alert[] Alerts { get; set; }

            public string EndpointNameOfReceivingEndpoint { get; set; }
        }

        bool AuditDataAvailable(MyContext context, MyContext c)
        {
            lock (context)
            {
                if (c.Message != null)
                {
                    return true;
                }

                if (c.MessageId == null)
                {
                    return false;
                }

                var message =
                    Get<Message>("/api/messages/" + context.MessageId + "-" + context.EndpointNameOfReceivingEndpoint);

                if (message == null)
                {
                    return false;
                }

                c.Message = message;

                return true;
            }
        }

        [Test]
        public void Should_be_imported_and_accessible_via_the_rest_api()
        {
            var context = new MyContext();

            Scenario.Define(context)
                .WithEndpoint<ManagementEndpoint>()
                .WithEndpoint<Sender>(b => b.Given(bus => bus.Send(new MyMessage())))
                .WithEndpoint<Receiver>()
                .Done(c => AuditDataAvailable(context, c))
                .Run();

            Assert.AreEqual(context.MessageId, context.Message.MessageId,
                "The returned message should match the processed one");
            Assert.AreEqual(MessageStatus.Failed, context.Message.Status, "Status should be set to failed");
            Assert.AreEqual(1, context.Message.FailureDetails.NumberOfTimesFailed, "Failed count should be 1");
            Assert.AreEqual("Simulated exception", context.Message.FailureDetails.Exception.Message,
                "Exception message should be captured");

        }

        [Test]
        public void Should_raise_an_alert()
        {
            var context = new MyContext();

            Scenario.Define(context)
                .WithEndpoint<ManagementEndpoint>()
                .WithEndpoint<Sender>(b => b.Given(bus => bus.Send(new MyMessage())))
                .WithEndpoint<Receiver>()
                .Done(c => AlertDataAvailable(context, c))
                .Run();

            Assert.IsTrue(context.Alerts.Length == 1, "Must store the alert in Raven database.");
            Assert.IsTrue(context.Alerts[0].Description.Contains("exception"), "For failed messages, the description should contain the exception information");
            var containsFailedMessageId = context.Alerts[0].RelatedTo.Any(item => item.Contains("/failedMessageId/"));
            Assert.IsTrue(containsFailedMessageId, "For failed message, the RelatedId must contain the api url to retrieve additional details about the failed message");
        }


        bool AlertDataAvailable(MyContext context, MyContext c)
        {
            var alerts = Get<Alert[]>("/api/alerts/");
            if (alerts == null || alerts.Length == 0)
            {
                System.Threading.Thread.Sleep(1000);
                return false;
            }
            c.Alerts = alerts;
            return true;
        }
    }
}