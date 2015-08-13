
namespace ServiceBus.Management.AcceptanceTests.Audit
{
    using System;
    using System.Collections.Generic;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Transports;
    using NUnit.Framework;
    using ServiceBus.Management.AcceptanceTests.Contexts;
    using ServiceControl.CompositeViews.Messages;

    public class When_a_message_sent_from_third_party_endpoint_with_missing_metadata : AcceptanceTest
    {
        [Test]
        public void Null_TimeSent_should_not_be_cast_to_DateTimeMin()
        {
            MessagesView auditedMessage = null;
            var context = new MyContext
            {
                MessageId = Guid.NewGuid().ToString()
            };

            Scenario.Define(context)
                .WithEndpoint<ManagementEndpoint>(c => c.AppConfig(PathToAppConfig))
                .WithEndpoint<ThridPartyEndpoint>()
                .Done(c => TryGetSingle("/api/messages?include_system_messages=false&sort=id", out auditedMessage, m => m.MessageId == c.MessageId))
                .Run(TimeSpan.FromSeconds(5));

            Assert.AreEqual(null, auditedMessage.TimeSent);
        }

        public class ThridPartyEndpoint : EndpointConfigurationBuilder
        {
            public ThridPartyEndpoint()
            {
                EndpointSetup<DefaultServerWithoutAudit>();
            }

            class SendMessage : IWantToRunWhenBusStartsAndStops
            {
                public ISendMessages SendMessages { get; set; }

                public MyContext MyContext { get; set; }

                public void Start()
                {
                    var transportMessage = new TransportMessage(MyContext.MessageId, new Dictionary<string, string>() { { Headers.ProcessingEndpoint, Configure.EndpointName } });
                    SendMessages.Send(transportMessage, Address.Parse("audit"));
                }

                public void Stop()
                {
                }
            }
        }

        class MyContext : ScenarioContext
        {
            public string MessageId { get; set; }
        }
    }
}

