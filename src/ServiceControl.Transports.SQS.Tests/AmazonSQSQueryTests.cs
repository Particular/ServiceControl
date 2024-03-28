namespace ServiceControl.Transport.Tests;

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using NServiceBus;
using NServiceBus.AcceptanceTesting;
using NUnit.Framework;
using Transports;
using Transports.SQS;

[TestFixture]
class AmazonSQSQueryTests : TransportTestFixture
{
    [Test]
    public async Task RunScenario()
    {
        // We need to wait a bit of time, to ensure AWS metrics are retrievable
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(6));
        CancellationToken token = cancellationTokenSource.Token;
        var provider = new FakeTimeProvider();
        provider.SetUtcNow(DateTimeOffset.UtcNow);
        var transportSettings = new TransportSettings
        {
            ConnectionString = configuration.ConnectionString,
            MaxConcurrency = 1,
            EndpointName = Guid.NewGuid().ToString("N")
        };
        const int messagesSent = 15;
        var query = new AmazonSQSQuery(provider);
        await Scenario.Define<MyContext>()
            .WithEndpoint(new Receiver(transportSettings.EndpointName), b =>
            b
                .CustomConfig(ec =>
                {
                    configuration.TransportCustomization.CustomizeEndpoint(ec, transportSettings);
                })
                .When(async bus =>
                {
                    for (int i = 0; i < messagesSent; i++)
                    {
                        await bus.SendLocal(new MyMessage());
                    }
                }))
            .Done(context => context.Counter == messagesSent)
            .Run();

        var dictionary = new Dictionary<string, string>();

        query.Initialise(dictionary.ToFrozenDictionary());

        await Task.Delay(TimeSpan.FromMinutes(2));

        var queueNames = new List<IBrokerQueue>();
        await foreach (IBrokerQueue queueName in query.GetQueueNames(token))
        {
            queueNames.Add(queueName);
        }

        IBrokerQueue queue = queueNames.Find(name => name.QueueName == transportSettings.EndpointName);
        Assert.IsNotNull(queue);

        long total = 0L;

        DateTime startDate = provider.GetUtcNow().DateTime;
        provider.Advance(TimeSpan.FromDays(1));
        await foreach (QueueThroughput queueThroughput in query.GetThroughputPerDay(queue, DateOnly.FromDateTime(startDate), token))
        {
            total += queueThroughput.TotalThroughput;
        }

        Assert.AreEqual(messagesSent, total);
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
                testContext.Counter++;
                return Task.CompletedTask;
            }
        }
    }

    class MyMessage : ICommand;

    class MyContext : ScenarioContext
    {
        public int Counter { get; set; }
    }
}