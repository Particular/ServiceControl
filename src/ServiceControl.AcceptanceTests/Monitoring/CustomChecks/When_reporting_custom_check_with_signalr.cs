namespace ServiceControl.AcceptanceTests.Monitoring.CustomChecks
{
    using System;
    using System.Net.Http;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using AcceptanceTesting.EndpointTemplates;
    using Microsoft.AspNetCore.SignalR.Client;
    using Microsoft.Extensions.DependencyInjection;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.CustomChecks;
    using NServiceBus.Features;
    using NUnit.Framework;
    using ServiceBus.Management.Infrastructure.Settings;

    [TestFixture]
    class When_reporting_custom_check_with_signalr : AcceptanceTest
    {
        [Test]
        [CancelAfter(120_000)]
        public async Task Should_result_in_a_custom_check_failed_event(CancellationToken cancellation)
        {
            var context = await Define<MyContext>(ctx =>
                {
                    ctx.HttpMessageHandlerFactory = () => HttpMessageHandlerFactory();
                })
                .WithEndpoint<EndpointWithCustomCheck>()
                .WithEndpoint<EndpointThatUsesSignalR>()
                .Done(c => c.SignalrEventReceived)
                .Run(cancellation);

            Assert.That(context.SignalrData.IndexOf("\"severity\":\"error\","), Is.GreaterThan(0), "Couldn't find severity error in signalr data");
        }

        public class MyContext : ScenarioContext
        {
            public bool SignalrEventReceived { get; set; }
            public Func<HttpMessageHandler> HttpMessageHandlerFactory { get; set; }
            public string SignalrData { get; set; }
        }

        public class EndpointThatUsesSignalR : EndpointConfigurationBuilder
        {
            public EndpointThatUsesSignalR() => EndpointSetup<DefaultServerWithoutAudit>(c => c.EnableFeature<EnableSignalR>());

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

                void EnvelopeReceived(JsonElement jElement)
                {
                    var s = jElement.ToString();
                    if (s.IndexOf("\"EventLogItemAdded\"", StringComparison.Ordinal) <= 0 ||
                        s.IndexOf("EventLogItem/CustomChecks/CustomCheckFailed", StringComparison.Ordinal) <= 0)
                    {
                        return;
                    }

                    context.SignalrData = s;
                    context.SignalrEventReceived = true;
                }

                protected override Task OnStart(IMessageSession session, CancellationToken cancellationToken = default)
                {
                    // We might also be able to strongly type this to match instead of just getting a string?
                    connection.On<JsonElement>("PushEnvelope", EnvelopeReceived);

                    return connection.StartAsync(cancellationToken);
                }

                protected override Task OnStop(IMessageSession session, CancellationToken cancellationToken = default) => connection.StopAsync(cancellationToken);

                readonly MyContext context;
                readonly HubConnection connection;
            }
        }

        class EndpointWithCustomCheck : EndpointConfigurationBuilder
        {
            public EndpointWithCustomCheck() => EndpointSetup<DefaultServerWithoutAudit>(c => { c.ReportCustomChecksTo(Settings.DEFAULT_INSTANCE_NAME, TimeSpan.FromSeconds(1)); });

            public class EventuallyFailingCustomCheck()
                : CustomCheck("EventuallyFailingCustomCheck", "Testing", TimeSpan.FromSeconds(1))
            {
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