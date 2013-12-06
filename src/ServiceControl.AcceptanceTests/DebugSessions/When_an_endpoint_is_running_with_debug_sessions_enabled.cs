namespace ServiceBus.Management.AcceptanceTests
{
    using System;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using Contexts;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using ServiceControl.MessageAuditing;

    public class When_an_endpoint_is_running_with_debug_sessions_enabled : AcceptanceTest
    {

        [Test,Explicit("Until we can fixed the missing file raven issue")]
        public void Debug_session_id_should_be_present_in_header()
        {
            var context = new MyContext();

            Scenario.Define(context)
                .WithEndpoint<ManagementEndpoint>(c => c.AppConfig(PathToAppConfig))
                .WithEndpoint<Sender>(b => b.Given((bus, c) =>
                {
                    c.EndpointNameOfSendingEndpoint = Configure.EndpointName;
                    bus.Send(new MyMessage());
                }))
                .WithEndpoint<Receiver>()
                .Done(c => AuditDataAvailable(context, c))
                .Run(TimeSpan.FromSeconds(40));

            Assert.NotNull(context.ReturnedMessage, "No message was returned by the management api");
            Assert.AreEqual(context.MessageId, context.ReturnedMessage.Headers.SingleOrDefault(kvp => kvp.Key == "ServiceControl.DebugSessionId").Value);

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

            public AuditMessage ReturnedMessage { get; set; }

            public string EndpointNameOfReceivingEndpoint { get; set; }

            public string EndpointNameOfSendingEndpoint { get; set; }
        }


        bool AuditDataAvailable(MyContext context, MyContext c)
        {
            lock (context)
            {
                if (c.ReturnedMessage != null)
                {
                    return true;
                }

                if (c.MessageId == null)
                {
                    return false;
                }

                c.ReturnedMessage = Get<AuditMessage>("/api/messages/" + context.MessageId + "-" +
                                 context.EndpointNameOfReceivingEndpoint);

                if (c.ReturnedMessage == null)
                {
                    return false;
                }

                return true;
            }
        }

       

       
    }
}