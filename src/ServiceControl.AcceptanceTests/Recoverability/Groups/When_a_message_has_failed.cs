namespace ServiceControl.AcceptanceTests.Recoverability.Groups
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Features;
    using NUnit.Framework;
    using ServiceBus.Management.AcceptanceTests;
    using ServiceBus.Management.AcceptanceTests.Contexts;
    using ServiceControl.Groups;
    using ServiceControl.Infrastructure;
    using ServiceControl.MessageFailures;

    public class When_a_message_has_failed : AcceptanceTest
    {
        [Test]
        public void Should_be_grouped()
        {
            var context = new MyContext();

            MessageFailureHistory failedMessage = null;

            Scenario.Define(context)
                .WithEndpoint<ManagementEndpoint>(c => c.AppConfig(PathToAppConfig))
                .WithEndpoint<Receiver>(b => b.Given(bus => bus.SendLocal(new MyMessage())))
                .Done(c => c.MessageId != null && TryGet("/api/errors/" + c.UniqueMessageId, out failedMessage, msg => msg.FailureGroups.Any()))
                .Run();

            Assert.AreEqual(context.UniqueMessageId, failedMessage.UniqueMessageId);

            Assert.IsNotEmpty(failedMessage.FailureGroups, "The returned message should have failure groups");
        }

        [Test]
        public void Groups_should_be_updated()
        {
            var context = new MyContext();

            List<FailureGroup> groups = null;
            MessageFailureHistory failedMessage = null;

            Scenario.Define(context)
                .WithEndpoint<ManagementEndpoint>(c => c.AppConfig(PathToAppConfig))
                .WithEndpoint<Receiver>(b => b.Given(bus => bus.SendLocal(new MyMessage())))
                .Done(c =>
                {
                    if (c.MessageId == null)
                        return false;

                    if (!TryGet("/api/errors/" + c.UniqueMessageId, out failedMessage, msg => msg.FailureGroups.Any()))
                        return false;
                    
                    if (!(TryGetMany("/api/recoverability/groups/", out groups) && groups.Any()))
                        return false;

                    return true;
                })
                .Run();

            Assert.IsNotEmpty(groups, "There should be failure groups created");
            var lastAttempt = failedMessage.ProcessingAttempts.OrderByDescending(x => x.AttemptedAt).First();
            foreach (var failureGroup in groups)
            {
                Assert.AreEqual(1, failureGroup.Count, "Failure Group should have one element in it " + failureGroup.Title);

                Assert.AreEqual(lastAttempt.AttemptedAt,  failureGroup.First, "Failure Group should have started when the first message arrived " + failureGroup.Title);
                Assert.AreEqual(lastAttempt.AttemptedAt, failureGroup.Last, "Failure Group should have end set to when the most recent message arrived " + failureGroup.Title);
            }
        }


        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultServerWithoutAudit>(c => Configure.Features.Disable<SecondLevelRetries>())
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
