namespace ServiceBus.Management.AcceptanceTests
{
    using System;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Transports;
    using NUnit.Framework;
    using ServiceBus.Management.AcceptanceTests.Contexts;
    using ServiceControl.CompositeViews.Messages;

    public class When_a_message_has_been_successfully_processed_from_sendonly: AcceptanceTest
    {
        [Test]
        public void Should_import_messages_from_sendonly_endpoint()
        {
            var context = new MyContext
            {
                MessageId = Guid.NewGuid().ToString()
            };

            Scenario.Define(context)
                .WithEndpoint<ManagementEndpoint>(c => c.AppConfig(PathToAppConfig))
                .WithEndpoint<SendOnlyEndpoint>()
                .Done(c =>
                {
                    MessagesView auditedMessage;
                    if (!TryGetSingle("/api/messages?include_system_messages=false&sort=id", out auditedMessage, m => m.MessageId == c.MessageId))
                    {
                        return false;
                    }
                    return true;
                })
                .Run(TimeSpan.FromSeconds(40));
        }

        public class SendOnlyEndpoint : EndpointConfigurationBuilder
        {
            public SendOnlyEndpoint()
            {
                EndpointSetup<DefaultServerWithoutAudit>();
            }

            class SendMessage : IWantToRunWhenBusStartsAndStops
            {
                public ISendMessages SendMessages { get; set; }

                public MyContext MyContext { get; set; }

                public void Start()
                {
                    //hack until we can fix the types filtering in default server
                    if (MyContext == null || string.IsNullOrEmpty(MyContext.MessageId))
                    {
                        return;
                    }

                    if (Configure.EndpointName != "Particular.ServiceControl")
                    {
                        return;
                    }

                    var transportMessage = new TransportMessage();
                    transportMessage.Headers[Headers.MessageId] = MyContext.MessageId;
                    transportMessage.Headers[Headers.ProcessingEndpoint] = Configure.EndpointName;
                    SendMessages.Send(transportMessage, Address.Parse("audit"));
                }

                public void Stop()
                {
                }
            }
        }

        public class MyContext : ScenarioContext
        {
            public string MessageId { get; set; }

            public string EndpointNameOfReceivingEndpoint { get; set; }

            public string EndpointNameOfSendingEndpoint { get; set; }

            public string PropertyToSearchFor { get; set; }
        }
    }
}