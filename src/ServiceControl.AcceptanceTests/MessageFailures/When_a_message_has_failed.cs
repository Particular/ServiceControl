﻿namespace ServiceBus.Management.AcceptanceTests
{
    using System;
    using System.Linq;
    using Contexts;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Features;
    using NUnit.Framework;
    using ServiceControl.Contracts.MessageFailures;
    using ServiceControl.Contracts.Operations;
    using ServiceControl.EventLog;
    using ServiceControl.Infrastructure;
    using ServiceControl.MessageFailures;
    using ServiceControl.MessageFailures.Api;

    public class When_a_message_has_failed : AcceptanceTest
    {

        [Test]
        public void Should_be_imported_and_accessible_via_the_rest_api()
        {
            var context = new MyContext();

            MessageFailureHistory failedMessage = null;

            Scenario.Define(context)
                .WithEndpoint<ManagementEndpoint>(c => c.AppConfig(PathToAppConfig))
                .WithEndpoint<Receiver>(b => b.Given(bus => bus.SendLocal(new MyMessage())))
                .Done(c => c.MessageId != null && TryGet("/api/errors/" + c.UniqueMessageId, out failedMessage))
                .Run();

            Assert.AreEqual(context.UniqueMessageId, failedMessage.UniqueMessageId);
         
           // The message Ids may contain a \ if they are from older versions. 
            Assert.AreEqual(context.MessageId, failedMessage.ProcessingAttempts.Last().MessageId,
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

            FailedMessageView failure = null;

            Scenario.Define(context)
                .WithEndpoint<ManagementEndpoint>(c => c.AppConfig(PathToAppConfig))
                .WithEndpoint<Receiver>(b => b.Given(bus => bus.SendLocal(new MyMessage())))
                .Done(c => TryGetSingle("/api/errors", out failure, r => r.MessageId == c.MessageId))
                .Run();

          

            // The message Ids may contain a \ if they are from older versions. 
            Assert.AreEqual(context.MessageId, failure.MessageId.Replace(@"\", "-"), "The returned message should match the processed one");
            Assert.AreEqual(FailedMessageStatus.Unresolved, failure.Status, "Status of new messages should be failed");
            Assert.AreEqual(1, failure.NumberOfProcessingAttempts, "One attempt should be stored");
        }

        [Test]
        public void Should_add_an_event_log_item()
        {
            var context = new MyContext();

            EventLogItem entry = null;

            Scenario.Define(context)
                .WithEndpoint<ManagementEndpoint>(c => c.AppConfig(PathToAppConfig))
                .WithEndpoint<Receiver>(b => b.Given(bus => bus.SendLocal(new MyMessage())))
                .Done(c => TryGetSingle("/api/eventlogitems/", out entry, e => e.RelatedTo.Any(r => r.Contains(c.UniqueMessageId)) && e.EventType == typeof(MessageFailed).Name))
                .Run();

            Assert.AreEqual(Severity.Error,entry.Severity, "Failures shoudl be treated as errors");
            Assert.IsTrue(entry.Description.Contains("exception"), "For failed messages, the description should contain the exception information");
            Assert.IsTrue(entry.RelatedTo.Any(item => item == "/message/" + context.UniqueMessageId), "Should contain the api url to retrieve additional details about the failed message");
            Assert.IsTrue(entry.RelatedTo.Any(item => item == "/endpoint/" + context.EndpointNameOfReceivingEndpoint), "Should contain the api url to retrieve additional details about the endpoint where the message failed");
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
            public string EndpointNameOfReceivingEndpoint { get; set; }

            public string UniqueMessageId
            {
                get
                {
                    return DeterministicGuid.MakeId(MessageId, EndpointNameOfReceivingEndpoint).ToString();
                }
            }
        }
    }
}