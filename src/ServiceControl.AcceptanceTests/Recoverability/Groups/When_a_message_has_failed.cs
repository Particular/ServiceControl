namespace ServiceBus.Management.AcceptanceTests.Recoverability.Groups
{
    using System;
    using System.Linq;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Features;
    using NServiceBus.Settings;
    using NUnit.Framework;
    using ServiceBus.Management.AcceptanceTests.Contexts;
    using ServiceControl.Infrastructure;
    using ServiceControl.MessageFailures;
    using ServiceControl.Recoverability;

    class When_a_message_has_failed : AcceptanceTest
    {
        [Test]
        public void Should_be_grouped()
        {
            var context = new MyContext();

            FailedMessage failedMessage = null;

            Define(context)
                .WithEndpoint<Receiver>(b => b.Given(bus => bus.SendLocal(new MyMessage())))
                .Done(c =>
                {
                    if (c.MessageId == null)
                    {
                        return false;
                    }

                    return TryGet("/api/errors/" + c.UniqueMessageId, out failedMessage, msg => msg.FailureGroups.Any());
                })
                .Run();

            Assert.AreEqual(context.UniqueMessageId, failedMessage.UniqueMessageId);
            Assert.IsNotEmpty(failedMessage.FailureGroups, "The returned message should have failure groups");

            Assert.AreEqual(1, failedMessage.FailureGroups.Count(g => g.Type == ExceptionTypeAndStackTraceFailureClassifier.Id), $"{ExceptionTypeAndStackTraceFailureClassifier.Id} FailureGroup was not created");
            Assert.AreEqual(1, failedMessage.FailureGroups.Count(g => g.Type == MessageTypeFailureClassifier.Id), $"{MessageTypeFailureClassifier.Id} FailureGroup was not created");
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultServerWithoutAudit>(c => c.DisableFeature<SecondLevelRetries>());
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public MyContext Context { get; set; }

                public IBus Bus { get; set; }

                public ReadOnlySettings Settings { get; set; }

                public void Handle(MyMessage message)
                {
                    Context.EndpointNameOfReceivingEndpoint = Settings.EndpointName();
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

            public string UniqueMessageId => DeterministicGuid.MakeId(MessageId, EndpointNameOfReceivingEndpoint).ToString();
        }
    }
}