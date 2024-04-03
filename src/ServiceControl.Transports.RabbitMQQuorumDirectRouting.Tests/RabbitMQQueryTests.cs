namespace ServiceControl.Transport.Tests;

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using NServiceBus;
using NServiceBus.AcceptanceTesting;
using NServiceBus.AcceptanceTesting.Support;
using NUnit.Framework;
using Transports;
using Transports.RabbitMQ;

[TestFixture]
class RabbitMQQueryTests : TransportTestFixture
{
    [Test]
    public async Task RunScenario()
    {
        // We need to wait a bit of time, because the scenario running takes on average 1 sec per run.
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        CancellationToken token = cancellationTokenSource.Token;
        var provider = new FakeTimeProvider();
        var transportSettings = new TransportSettings
        {
            ConnectionString = configuration.ConnectionString,
            MaxConcurrency = 1,
            EndpointName = Guid.NewGuid().ToString("N")
        };
        var totalWrapper = new TotalWrapper();
        var query = new RabbitMQQuery(provider, transportSettings);
        IScenarioWithEndpointBehavior<MyContext> scenario = Scenario.Define<MyContext>(c =>
            {
                c.Total = totalWrapper;
            })
            .WithEndpoint(new Receiver(transportSettings.EndpointName), b =>
            b
                .CustomConfig(ec =>
                {
                    configuration.TransportCustomization.CustomizeEndpoint(ec, transportSettings);
                })
                .When(bus => bus.SendLocal(new MyMessage())))
            .Done(context => context.ResetEvent.IsSet);
        await configuration.TransportCustomization.ProvisionQueues(transportSettings, []);

        var dictionary = new Dictionary<string, string>();
        query.Initialise(dictionary.ToFrozenDictionary());

        var queueNames = new List<IBrokerQueue>();
        await foreach (IBrokerQueue queueName in query.GetQueueNames(token))
        {
            queueNames.Add(queueName);
        }

        IBrokerQueue queue = queueNames.Find(name => name.QueueName == transportSettings.EndpointName);
        Assert.IsNotNull(queue);

        long total = 0L;
        using var reset = new ManualResetEventSlim();

        var runScenarioAndAdvanceTime = Task.Run(async () =>
        {
            while (!reset.IsSet)
            {
                _ = await scenario.Run();
                provider.Advance(TimeSpan.FromMinutes(15));
            }
        }, token);

        await foreach (QueueThroughput queueThroughput in query.GetThroughputPerDay(queue, new DateOnly(), token))
        {
            total += queueThroughput.TotalThroughput;
        }

        reset.Set();
        await runScenarioAndAdvanceTime.WaitAsync(token);

        // Asserting that we have one message per hour during 24 hours, the first snapshot is not counted hence the 23 assertion. 
        Assert.Greater(total, 23);
        Assert.LessOrEqual(total, totalWrapper.Total);
    }

    class Receiver : EndpointConfigurationBuilder
    {
        public Receiver(string endpointName) => EndpointSetup<BasicEndpointSetup>(c =>
        {
            c.EnableInstallers();
            c.UsePersistence<NonDurablePersistence>();
        }).CustomEndpointName(endpointName);

        public class MyMessageHandler(MyContext testContext) : IHandleMessages<MyMessage>
        {
            public Task Handle(MyMessage message, IMessageHandlerContext context)
            {
                testContext.ResetEvent.Set();
                testContext.Total.Total++;
                return Task.CompletedTask;
            }
        }
    }

    class MyMessage : ICommand;

    class TotalWrapper
    {
        public int Total { get; set; }
    }

    class MyContext : ScenarioContext
    {
        public ManualResetEventSlim ResetEvent { get; } = new(false);
        public TotalWrapper Total { get; set; }
    }
}