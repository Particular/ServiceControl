namespace ServiceBus.Management.AcceptanceTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Contexts;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Features;
    using NUnit.Framework;
    using ServiceControl.EventLog;
    using ServiceControl.MessageFailures;

    public class When_a_message_has_failed : AcceptanceTest
    {

        [Test]
        public void Should_be_imported_and_accessible_via_the_rest_api()
        {
            var context = new MyContext();

            Scenario.Define(context)
                .WithEndpoint<ManagementEndpoint>(c => c.AppConfig(PathToAppConfig))
                .WithEndpoint<Receiver>(b => b.Given(bus => bus.SendLocal(new MyMessage())))
                .Done(c => IsErrorMessageStored(context, c))
                .Run();

           // The message Ids may contain a \ if they are from older versions. 
            Assert.AreEqual(context.MessageId, context.FailedMessage.MessageId.Replace(@"\", "-"),
                "The returned message should match the processed one");
            Assert.AreEqual(MessageStatus.Failed, context.FailedMessage.Status, "Status should be set to failed");
            Assert.AreEqual(1, context.FailedMessage.ProcessingAttempts.Count(), "Failed count should be 1");
            Assert.AreEqual("Simulated exception", context.FailedMessage.ProcessingAttempts.Single().FailureDetails.Exception.Message,
                "Exception message should be captured");

        }

        [Test]
        public void Should_add_an_event_log_item()
        {
            var context = new MyContext();

            Scenario.Define(context)
                .WithEndpoint<ManagementEndpoint>(c => c.AppConfig(PathToAppConfig))
                  .WithEndpoint<Receiver>(b => b.Given(bus => bus.SendLocal(new MyMessage())))
                .WithEndpoint<Receiver>()
                .Done(IsEventLogDataAvailable)
                .Run();

            Assert.AreEqual(1, context.LogEntries.Count);
            Assert.IsTrue(context.LogEntries[0].Description.Contains("exception"), "For failed messages, the description should contain the exception information");
            var containsFailedMessageId = context.LogEntries[0].RelatedTo.Any(item => item.Contains("/failedMessageId/"));
            Assert.IsTrue(containsFailedMessageId, "For failed message, the RelatedId must contain the api url to retrieve additional details about the failed message");
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultServerWithoutAudit>(c=>Configure.Features.Disable<SecondLevelRetries>())
                    .AuditTo(Address.Parse("audit"));
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public MyContext Context { get; set; }

                public IBus Bus { get; set; }

                public void Handle(MyMessage message)
                {
                    Context.EndpointNameOfReceivingEndpoint = Configure.EndpointName;
                    Context.MessageId = Bus.CurrentMessageContext.Id.Replace(@"\", "-");
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
            public FailedMessage FailedMessage { get; set; }
            public List<EventLogItem> LogEntries { get; set; }
            public string EndpointNameOfReceivingEndpoint { get; set; }
        }

        bool IsErrorMessageStored(MyContext context, MyContext c)
        {
            lock (context)
            {
                if (c.FailedMessage != null)
                {
                    return true;
                }

                if (c.MessageId == null)
                {
                    return false;
                }

                var message =
                    Get<FailedMessage>("/api/errors/" + context.MessageId + "-" + context.EndpointNameOfReceivingEndpoint);

                if (message == null)
                {
                    return false;
                }

                c.FailedMessage = message;

                return true;
            }
        }




        bool IsEventLogDataAvailable(MyContext c)
        {
            var logEntries = Get<EventLogItem[]>("/api/eventlogitems/");
            if (logEntries == null || logEntries.Length == 0)
            {
                System.Threading.Thread.Sleep(5000);
                return false;
            }
            c.LogEntries = logEntries.ToList();
            return true;
        }
    }
}