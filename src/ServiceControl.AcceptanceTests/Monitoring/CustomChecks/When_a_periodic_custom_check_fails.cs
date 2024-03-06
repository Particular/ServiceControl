﻿namespace ServiceControl.AcceptanceTests.Monitoring.CustomChecks
{
    using System;
    using System.Linq;
    using System.Net.Http;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Contracts.CustomChecks;
    using EventLog;
    using Microsoft.AspNetCore.SignalR.Client;
    using Microsoft.Extensions.DependencyInjection;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.CustomChecks;
    using NServiceBus.Features;
    using NUnit.Framework;
    using ServiceBus.Management.Infrastructure.Settings;
    using TestSupport.EndpointTemplates;
    using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

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
                    ctx =>
                    {
                        ctx.HttpMessageHandlerFactory = () => HttpMessageHandlerFactory();
                    })
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
            public Func<HttpMessageHandler> HttpMessageHandlerFactory { get; set; }
            public bool SignalrStarted { get; set; }
        }

        public class EndpointThatUsesSignalR : EndpointConfigurationBuilder
        {
            public EndpointThatUsesSignalR() => EndpointSetup<DefaultServer>(c => c.EnableFeature<EnableSignalR>());

            class EnableSignalR : Feature
            {
                protected override void Setup(FeatureConfigurationContext context)
                {
                    context.Services.AddSingleton<SignalrStarter>();
                    context.RegisterStartupTask(provider => provider.GetRequiredService<SignalrStarter>());
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
                        if (s.IndexOf("\"CustomCheckFailed\"", StringComparison.Ordinal) <= 0)
                        {
                            return;
                        }

                        context.SignalrData = s;
                        context.SignalrEventReceived = true;
                    }

                    protected override async Task OnStart(IMessageSession session, CancellationToken cancellationToken = default)
                    {
                        // We might also be able to strongly type this to match instead of just getting a string?
                        connection.On<JsonElement>("PushEnvelope", EnvelopeReceived);

                        await connection.StartAsync(cancellationToken);

                        context.SignalrStarted = connection.State == HubConnectionState.Connected;
                    }

                    protected override Task OnStop(IMessageSession session, CancellationToken cancellationToken = default) => connection.StopAsync(cancellationToken);

                    readonly MyContext context;
                    readonly HubConnection connection;
                }
            }
        }

        public class WithCustomCheck : EndpointConfigurationBuilder
        {
            public WithCustomCheck() => EndpointSetup<DefaultServer>(c => { c.ReportCustomChecksTo(Settings.DEFAULT_SERVICE_NAME, TimeSpan.FromSeconds(1)); });

            class FailingCustomCheck : NServiceBus.CustomChecks.CustomCheck
            {
                public FailingCustomCheck(MyContext context) : base("MyCustomCheckId", "MyCategory", TimeSpan.FromSeconds(5)) => this.context = context;

                public override Task<CheckResult> PerformCheck(CancellationToken cancellationToken = default)
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