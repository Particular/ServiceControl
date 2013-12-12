﻿namespace ServiceBus.Management.AcceptanceTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Contexts;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Features;
    using NUnit.Framework;
    using ServiceControl.CompositeViews;
    using ServiceControl.Contracts.Operations;
    using ServiceControl.EventLog;
    using ServiceControl.MessageFailures;
    using ServiceControl.MessageFailures.Api;

    public class When_a_message_has_failed : AcceptanceTest
    {

        [Test]
        public void Should_be_imported_and_accessible_via_the_rest_api()
        {
            var context = new MyContext();

            FailedMessage failedMessage = null;

            Scenario.Define(context)
                .WithEndpoint<ManagementEndpoint>(c => c.AppConfig(PathToAppConfig))
                .WithEndpoint<Receiver>(b => b.Given(bus => bus.SendLocal(new MyMessage())))
                .Done(c => c.MessageId != null && TryGet("/api/errors/" + c.UniqueMessageId, out failedMessage))
                .Run();

           // The message Ids may contain a \ if they are from older versions. 
            Assert.AreEqual(context.MessageId, failedMessage.MostRecentAttempt.MessageId,
                "The returned message should match the processed one");
            Assert.AreEqual(FailedMessageStatus.Unresolved, failedMessage.Status, "Status should be set to unresolved");
            Assert.AreEqual(1, failedMessage.ProcessingAttempts.Count(), "Failed count should be 1");
            Assert.AreEqual("Simulated exception", failedMessage.ProcessingAttempts.Single().FailureDetails.Exception.Message,
                "Exception message should be captured");

        }

        [Test]
        public void Should_be_listed_in_the_error_list()
        {
            var context = new MyContext();

            var response = new List<FailedMessageView>();

            Scenario.Define(context)
                .WithEndpoint<ManagementEndpoint>(c => c.AppConfig(PathToAppConfig))
                .WithEndpoint<Receiver>(b => b.Given(bus => bus.SendLocal(new MyMessage())))
                .Done(c => TryGetMany("/api/errors",out response))
                .Run();

            var failure = response.Single(r=>r.MessageId == context.MessageId);

            // The message Ids may contain a \ if they are from older versions. 
            Assert.AreEqual(context.MessageId, failure.MessageId.Replace(@"\", "-"), "The returned message should match the processed one");
            Assert.AreEqual(FailedMessageStatus.Unresolved, failure.Status, "Status of new messages should be failed");
            Assert.AreEqual(1, failure.NumberOfProcessingAttempts, "One attempt should be stored");
        }


        [Test]
        public void Should_be_listed_in_the_messages_list()
        {
            var context = new MyContext();

            var response = new List<MessagesView>();

            Scenario.Define(context)
                .WithEndpoint<ManagementEndpoint>(c => c.AppConfig(PathToAppConfig))
                .WithEndpoint<Receiver>(b => b.Given(bus => bus.SendLocal(new MyMessage())))
                .Done(c => TryGetMany("/api/messages", out response))
                .Run();

            var failure = response.Single(r => r.Headers.SingleOrDefault(kvp=>kvp.Key==Headers.MessageId).Value == context.MessageId);

            Assert.AreEqual(MessageStatus.Failed, failure.Status, "Status of new messages should be failed");
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
            public List<EventLogItem> LogEntries { get; set; }
            public string EndpointNameOfReceivingEndpoint { get; set; }

            public string UniqueMessageId
            {
                get
                {
                    return string.Format("{0}-{1}", MessageId.Replace(@"\", "-"), EndpointNameOfReceivingEndpoint);
                }
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