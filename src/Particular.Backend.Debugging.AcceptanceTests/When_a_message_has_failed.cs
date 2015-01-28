namespace Particular.Backend.Debugging.AcceptanceTests
{
    using System;
    using global::ServiceControl.Contracts.Operations;
    using global::ServiceControl.Infrastructure;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Features;
    using NUnit.Framework;
    using Particular.Backend.Debugging.AcceptanceTests.Contexts;
    using Particular.Backend.Debugging.Api;

    public class When_a_message_has_failed : AcceptanceTest
    {

        [Test]
        public void Should_be_listed_in_the_messages_list()
        {
            var context = new MyContext();

            var failure = new MessagesView();

            Scenario.Define(context)
                .WithEndpoint<ManagementEndpoint>(c => c.AppConfig(PathToAppConfig))
                .WithEndpoint<Receiver>(b => b.Given(bus => bus.SendLocal(new MyMessage())))
                .Done(c => TryGetSingle("/api/messages", out failure,m=>m.MessageId == c.MessageId))
                .Run();

            Assert.AreEqual(context.UniqueMessageId, failure.Id, "The unique id should be returned");
            
            Assert.AreEqual(MessageStatus.Failed, failure.Status, "Status of new messages should be failed");
            Assert.AreEqual(context.EndpointNameOfReceivingEndpoint, failure.SendingEndpoint.Name);
            Assert.AreEqual(context.EndpointNameOfReceivingEndpoint, failure.ReceivingEndpoint.Name);

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