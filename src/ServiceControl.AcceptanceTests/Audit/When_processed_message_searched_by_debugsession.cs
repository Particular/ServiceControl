namespace ServiceBus.Management.AcceptanceTests
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Settings;
    using NUnit.Framework;
    using ServiceBus.Management.AcceptanceTests.Contexts;
    using ServiceControl.CompositeViews.Messages;

    public class When_processed_message_searched_by_debugsession : AcceptanceTest
    {
        [Test]
        public async Task Should_be_found()
        {
            var context = new MyContext();

            await Define(context)
                .WithEndpoint<Sender>(b => b.Given((bus, c) =>
                {
                    var message = new MyMessage();

                    bus.SetMessageHeader(message, "ServiceControl.DebugSessionId", "DANCO-WIN8@Application1@2014-01-26T11:33:51");

                    bus.Send(message);
                }))
                .WithEndpoint<Receiver>()
                .Done(async c => c.MessageId != null && await TryGetMany<MessagesView>("/api/messages/search/DANCO-WIN8@Application1@2014-01-26T11:33:51"))
                .Run(TimeSpan.FromSeconds(40));
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
                EndpointSetup<DefaultServerWithAudit>();
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public MyContext Context { get; set; }

                public IBus Bus { get; set; }

                public ReadOnlySettings Settings { get; set; }

                public void Handle(MyMessage message)
                {
                    Context.EndpointNameOfReceivingEndpoint = Settings.EndpointName();
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
        }
    }
}