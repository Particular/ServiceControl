namespace ServiceControl.Transport.Tests
{
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using ServiceControl.Transports;

    class NServiceBusConfigTests : TransportTestFixture
    {
        [Test]
        public async Task Should_be_able_to_create_send_only_endpoint()
        {
            var transportSettings = new TransportSettings
            {
                EndpointName = GetTestQueueName("send"),
                ConnectionString = configuration.ConnectionString,
                MaxConcurrency = 1
            };

            var ctx = await Scenario.Define<Context>()
                .WithEndpoint<SendingEndpoint>(c => c.CustomConfig(ec => configuration.TransportCustomization.CustomizeSendOnlyEndpoint(ec, transportSettings)))
                .Done(c => c.EndpointsStarted)
                .Run();

            Assert.IsTrue(ctx.EndpointsStarted);
        }


        public class Context : ScenarioContext
        {
            public bool SendingEndpointGotResponse { get; set; }
            public string ReplyToAddress { get; set; }
        }

        public class SendingEndpoint : EndpointConfigurationBuilder
        {
            public SendingEndpoint()
            {
                EndpointSetup<DefaultServer>();
            }
        }

        public class MyMessage : IMessage
        {
        }
    }
}