namespace ServiceControl.Transport.Tests
{
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Configuration.AdvancedExtensibility;
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
                .WithEndpoint<SendOnlyEndpoint>(c => c.CustomConfig(ec => configuration.TransportCustomization.CustomizeSendOnlyEndpoint(ec, transportSettings)))
                .Done(c => c.EndpointsStarted)
                .Run();

            Assert.IsTrue(ctx.EndpointsStarted);
        }


        public class Context : ScenarioContext
        {
            public bool SendingEndpointGotResponse { get; set; }
            public string ReplyToAddress { get; set; }
        }

        public class SendOnlyEndpoint : EndpointConfigurationBuilder
        {
            public SendOnlyEndpoint()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    c.SendOnly();

                    //DisablePublishing API is available only on TransportExtensions for transports that implement IMessageDrivenPubSub so we need to set settings directly
                    c.GetSettings().Set("NServiceBus.PublishSubscribe.EnablePublishing", false);
                });
            }
        }

        public class MyMessage : IMessage
        {
        }
    }
}