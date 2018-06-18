namespace ServiceBus.Management.AcceptanceTests.CustomChecks
{
    using System;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Contexts;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using ServiceControl.Infrastructure.SignalR;
    using Microsoft.AspNet.SignalR.Client;
    using Microsoft.AspNet.SignalR.Client.Transports;
    using NServiceBus.CustomChecks;
    using NServiceBus.Features;
    using ServiceBus.Management.Infrastructure.Settings;

    [TestFixture]
    public class Custom_check_transition_should_trigger_signalr_event : AcceptanceTest
    {
        [Test]
        public async Task Should_result_in_a_custom_check_failed_event()
        {
            var context = await Define<MyContext>(ctx => { ctx.Handler = () => this.Handlers[Settings.DEFAULT_SERVICE_NAME]; })
                .WithEndpoint<EndpointWithFailingCustomCheck>()
                .WithEndpoint<EndpointThatUsesSignalR>()
                .Done(c => c.SignalrEventReceived)
                .Run(TimeSpan.FromMinutes(2));

            Assert.True(context.SignalrData.IndexOf("\"severity\": \"error\",") > 0);
        }

        public class MyContext : ScenarioContext
        {
            public bool SignalrEventReceived { get; set; }
            public Func<HttpMessageHandler> Handler { get; set; }
            public string SignalrData { get; set; }
        }

        public class EndpointThatUsesSignalR : EndpointConfigurationBuilder
        {
            public EndpointThatUsesSignalR()
            {
                EndpointSetup<DefaultServerWithoutAudit>(c => c.EnableFeature<EnableSignalR>());
            }

            class EnableSignalR : Feature
            {
                protected override void Setup(FeatureConfigurationContext context)
                {
                    context.RegisterStartupTask(b => b.Build<SignalrStarter>());
                }
            }

            class SignalrStarter : FeatureStartupTask
            {
                private readonly MyContext context;
                Connection connection;

                public SignalrStarter(MyContext context)
                {
                    this.context = context;
                    connection = new Connection("http://localhost/api/messagestream")
                    {
                        JsonSerializer = Newtonsoft.Json.JsonSerializer.Create(SerializationSettingsFactoryForSignalR.CreateDefault())
                    };
                }

                private void ConnectionOnReceived(string s)
                {
                    if (s.IndexOf("\"EventLogItemAdded\"") > 0)
                    {
                        if (s.IndexOf("EventLogItem/CustomChecks/CustomCheckFailed") > 0)
                        {
                            context.SignalrData = s;
                            context.SignalrEventReceived = true;
                        }
                    }
                }

                protected override Task OnStart(IMessageSession session)
                {
                    connection.Received += ConnectionOnReceived;

                    return connection.Start(new ServerSentEventsTransport(new SignalRHttpClient(context.Handler())));
                }

                protected override Task OnStop(IMessageSession session)
                {
                    connection.Stop();
                    return Task.FromResult(0);
                }
            }
        }

        public class EndpointWithFailingCustomCheck : EndpointConfigurationBuilder
        {

            public EndpointWithFailingCustomCheck()
            {
                EndpointSetup<DefaultServerWithoutAudit>(c => { c.ReportCustomChecksTo(Settings.DEFAULT_SERVICE_NAME, TimeSpan.FromSeconds(1)); });
            }

            public class EventuallyFailingCustomCheck : CustomCheck
            {
                private static int counter;

                public EventuallyFailingCustomCheck()
                    : base("EventuallyFailingCustomCheck", "Testing", TimeSpan.FromSeconds(1)) { }

                public override Task<CheckResult> PerformCheck()
                {
                    if (Interlocked.Increment(ref counter) / 5 % 2 == 1)
                    {
                        return Task.FromResult(CheckResult.Failed("fail!"));
                    }
                    return Task.FromResult(CheckResult.Pass);
                }
            }
        }
    }
}
