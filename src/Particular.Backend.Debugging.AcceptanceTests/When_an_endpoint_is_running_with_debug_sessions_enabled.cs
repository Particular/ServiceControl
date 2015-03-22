namespace Particular.Backend.Debugging.AcceptanceTests
{
    using System;
    using System.Linq;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using Particular.Backend.Debugging.AcceptanceTests.Contexts;
    using Particular.Backend.Debugging.Api;

    public class When_a_custom_header_is_added_to_a_message : AcceptanceTest
    {

        [Test]//, Explicit("Until we can fixed the missing file raven issue")]
        public void It_is_present_in_the_audit_snapshot()
        {
            var context = new MyContext();
            MessagesView message = null;

            Scenario.Define(context)
                .WithEndpoint<ManagementEndpoint>(c => c.AppConfig(PathToAppConfig))
                .WithEndpoint<Sender>(b => b.Given((bus, c) =>
                {
                    c.EndpointNameOfSendingEndpoint = Configure.EndpointName;
                    var messsage = new MyMessage();

                    Headers.SetMessageHeader(messsage, "SomeHeader", "SomeValue");

                    bus.Send(messsage);
                }))
                .WithEndpoint<Receiver>()
                .Done(c => TryGetSingle("/api/messages", out message))
                .Run(TimeSpan.FromSeconds(40));

            Assert.NotNull(message, "No message was returned by the management api");
            Assert.AreEqual("SomeValue", message.Headers.SingleOrDefault(_ => _.Key == "SomeHeader").Value);

        }


        public class Sender : EndpointConfigurationBuilder
        {
            public Sender()
            {
                EndpointSetup<DefaultServerWithoutAudit>()
                    .AddMapping<MyMessage>(typeof(Receiver));
            }
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultServer>()
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

            public string EndpointNameOfSendingEndpoint { get; set; }
        }


    }
}