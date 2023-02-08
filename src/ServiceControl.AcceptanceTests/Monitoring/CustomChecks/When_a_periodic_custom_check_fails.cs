namespace ServiceControl.AcceptanceTests.Monitoring.CustomChecks
{
    using System;
    using System.Linq;
    using System.Net.Http;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Contracts.CustomChecks;
    using EventLog;
    using Infrastructure.SignalR;
    using Microsoft.AspNet.SignalR.Client;
    using Microsoft.AspNet.SignalR.Client.Transports;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.CustomChecks;
    using NServiceBus.Features;
    using NUnit.Framework;
    using ServiceBus.Management.Infrastructure.Settings;
    using TestSupport.EndpointTemplates;
    using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;
    using JsonSerializer = Newtonsoft.Json.JsonSerializer;

    [TestFixture]
    class When_a_periodic_custom_check_fails : AcceptanceTest
    {
        [Test]
        public async Task Should_result_in_a_custom_check_failed_event()
        {
            EventLogItem entry = null;

            await Define<MyContext>(ctx => { ctx.SignalrStarted = true; })
                .WithEndpoint<WithCustomCheck>()
                .Done(async c =>
                {
                    var result = await this.TryGetSingle<EventLogItem>("/api/eventlogitems/", e => e.EventType == nameof(CustomCheckFailed));
                    entry = result;
                    return result;
                })
                .Run();

            Assert.AreEqual(Severity.Error, entry.Severity, "Failed custom checks should be treated as error");
            Assert.IsTrue(entry.RelatedTo.Any(item => item == "/customcheck/MyCustomCheckId"));
            Assert.IsTrue(entry.RelatedTo.Any(item => item.StartsWith($"/endpoint/{Conventions.EndpointNamingConvention(typeof(WithCustomCheck))}")));
        }

        [Test]
        public async Task Should_raise_a_signalr_event()
        {
            var context = await Define<MyContext>(
                    ctx => { ctx.Handler = () => Handler; })
                .WithEndpoint<WithCustomCheck>()
                .WithEndpoint<EndpointThatUsesSignalR>()
                .Done(c => c.SignalrEventReceived)
                .Run();

            Assert.IsNotNull(context.SignalrData);
        }

        public class MyContext : ScenarioContext
        {
            public bool SignalrEventReceived { get; set; }
            public string SignalrData { get; set; }
            public Func<HttpMessageHandler> Handler { get; set; }
            public bool SignalrStarted { get; set; }
        }

        public class EndpointThatUsesSignalR : EndpointConfigurationBuilder
        {
            public EndpointThatUsesSignalR()
            {
                EndpointSetup<DefaultServer>(c => c.EnableFeature<EnableSignalR>());
            }

            class EnableSignalR : Feature
            {
                protected override void Setup(FeatureConfigurationContext context)
                {
                    context.Container.ConfigureComponent<SignalrStarter>(DependencyLifecycle.SingleInstance);
                    context.RegisterStartupTask(b => b.Build<SignalrStarter>());
                }

                class SignalrStarter : FeatureStartupTask
                {
                    public SignalrStarter(MyContext context)
                    {
                        this.context = context;
                        connection = new Connection("http://localhost/api/messagestream")
                        {
                            JsonSerializer = JsonSerializer.Create(SerializationSettingsFactoryForSignalR.CreateDefault())
                        };
                    }

                    void ConnectionOnReceived(string s)
                    {
                        if (s.IndexOf("\"CustomCheckFailed\"") > 0)
                        {
                            context.SignalrData = s;
                            context.SignalrEventReceived = true;
                        }
                    }

                    protected override Task OnStart(IMessageSession session)
                    {
                        connection.Received += ConnectionOnReceived;
                        connection.StateChanged += change => { context.SignalrStarted = change.NewState == ConnectionState.Connected; };

                        return connection.Start(new ServerSentEventsTransport(new SignalRHttpClient(context.Handler())));
                    }

                    protected override Task OnStop(IMessageSession session)
                    {
                        connection.Stop();
                        return Task.FromResult(0);
                    }

                    readonly MyContext context;
                    Connection connection;
                }
            }
        }

        public class WithCustomCheck : EndpointConfigurationBuilder
        {
            public WithCustomCheck()
            {
                EndpointSetup<DefaultServer>(c => { c.ReportCustomChecksTo(Settings.DEFAULT_SERVICE_NAME, TimeSpan.FromSeconds(1)); });
            }

            class FailingCustomCheck : CustomCheck
            {
                public FailingCustomCheck(MyContext context) : base("MyCustomCheckId", "MyCategory", TimeSpan.FromSeconds(5))
                {
                    this.context = context;
                }

                public override Task<CheckResult> PerformCheck()
                {
                    if (executed && context.SignalrStarted)
                    {
                        return Task.FromResult(CheckResult.Failed("Some reason"));
                    }

                    executed = true;

                    return Task.FromResult(CheckResult.Pass);
                }

                readonly MyContext context;
                bool executed;
            }
        }
    }
}