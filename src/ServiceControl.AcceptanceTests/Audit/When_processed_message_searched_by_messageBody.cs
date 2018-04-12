namespace ServiceBus.Management.AcceptanceTests
{
    using System;
    using System.Collections.Generic;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Settings;
    using NUnit.Framework;
    using ServiceBus.Management.AcceptanceTests.Contexts;
    using ServiceControl.CompositeViews.Messages;

    public class When_processed_message_searched_by_messageBody : AcceptanceTest
    {
        [Test]
        public void Should_be_found_()
        {
            var context = new MyContext
            {
                PropertyToSearchFor = Guid.NewGuid().ToString()
            };

            List<MessagesView> response;

            Define(context)
                .WithEndpoint<Sender>(b => b.Given((bus, c) =>
                {
                    bus.Send(new MyMessage
                    {
                        PropertyToSearchFor = c.PropertyToSearchFor
                    });
                }))
                .WithEndpoint<Receiver>()
                .Done(c => TryGetMany("/api/messages/search/" + c.PropertyToSearchFor, out response))
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
            public string PropertyToSearchFor { get; set; }
        }

        public class MyContext : ScenarioContext
        {
            public string MessageId { get; set; }

            public string EndpointNameOfReceivingEndpoint { get; set; }

            public string PropertyToSearchFor { get; set; }
        }
    }
}