namespace ServiceControl.AcceptanceTests.Monitoring.CustomChecks
{
    using System;
    using System.Net.Http;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.SignalR.Client;
    using Microsoft.Extensions.DependencyInjection;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.CustomChecks;
    using NServiceBus.Features;
    using NUnit.Framework;
    using ServiceBus.Management.Infrastructure.Settings;
    using TestSupport.EndpointTemplates;

    [TestFixture]
    class When_reporting_custom_check_with_signalr : AcceptanceTest
    {
        [Test]
        public async Task Should_result_in_a_custom_check_failed_event()
        {
            var context = await Define<MyContext>(ctx =>
                {
                    ctx.HttpMessageHandlerFactory = () => HttpMessageHandlerFactory();
                })
                .WithEndpoint<EndpointWithCustomCheck>()
                .WithEndpoint<EndpointThatUsesSignalR>()
                .Done(c => c.SignalrEventReceived)
                .Run(TimeSpan.FromMinutes(2));

            Assert.True(context.SignalrData.IndexOf("\"severity\":\"error\",") > 0, "Couldn't find severity error in signalr data");
        }

        public class MyContext : ScenarioContext
        {
            public bool SignalrEventReceived { get; set; }
            public Func<HttpMessageHandler> HttpMessageHandlerFactory { get; set; }
            public string SignalrData { get; set; }
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
                    context.Services.AddSingleton<SignalrStarter>();
                    context.RegisterStartupTask(b => b.GetRequiredService<SignalrStarter>());
                }
            }

            class SignalrStarter : FeatureStartupTask
            {
                public SignalrStarter(MyContext context)
                {
                    this.context = context;
                    connection = new HubConnectionBuilder()
                        .WithUrl("http://localhost/api/messagestream", o => o.HttpMessageHandlerFactory = _ => context.HttpMessageHandlerFactory())
                        .Build();
                }

                // TODO rename to better match what this is actually doing
                void ConnectionOnReceived(JsonElement jElement)
                {
                    var s = jElement.ToString();
                    if (s.IndexOf("\"EventLogItemAdded\"") > 0)
                    {
                        if (s.IndexOf("EventLogItem/CustomChecks/CustomCheckFailed") > 0)
                        {
                            context.SignalrData = s;
                            context.SignalrEventReceived = true;
                        }
                    }
                }

                protected override Task OnStart(IMessageSession session, CancellationToken cancellationToken = default)
                {
                    // TODO Align this name with the one chosen for GlobalEventHandler
                    // We might also be able to strongly type this to match instead of just getting a string?
                    connection.On<JsonElement>("PushEnvelope", ConnectionOnReceived);

                    return connection.StartAsync(cancellationToken);
                }

                protected override Task OnStop(IMessageSession session, CancellationToken cancellationToken = default)
                {
                    return connection.StopAsync(cancellationToken);
                }

                readonly MyContext context;
                readonly HubConnection connection;
            }
        }

        class EndpointWithCustomCheck : EndpointConfigurationBuilder
        {
            public EndpointWithCustomCheck()
            {
                EndpointSetup<DefaultServer>(c => { c.ReportCustomChecksTo(Settings.DEFAULT_SERVICE_NAME, TimeSpan.FromSeconds(1)); });
            }

            public class EventuallyFailingCustomCheck : CustomCheck
            {
                public EventuallyFailingCustomCheck()
                    : base("EventuallyFailingCustomCheck", "Testing", TimeSpan.FromSeconds(1))
                {
                }

                public override Task<CheckResult> PerformCheck(CancellationToken cancellationToken = default)
                {
#pragma warning disable IDE0047 // Remove unnecessary parentheses
                    if ((Interlocked.Increment(ref counter) / 5) % 2 == 1)
#pragma warning restore IDE0047 // Remove unnecessary parentheses
                    {
                        return Task.FromResult(CheckResult.Failed("fail!"));
                    }

                    return Task.FromResult(CheckResult.Pass);
                }

                static int counter;
            }
        }
    }
}