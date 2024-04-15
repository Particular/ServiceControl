namespace ServiceControl.Transport.Tests;

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NServiceBus;
using NServiceBus.AcceptanceTesting;
using NUnit.Framework;
using Particular.Approvals;
using Transports;
using Transports.SQS;

[TestFixture]
class AmazonSQSQueryTests : TransportTestFixture
{
    FakeTimeProvider provider;
    TransportSettings transportSettings;
    AmazonSQSQuery query;

    [SetUp]
    public void Initialise()
    {
        provider = new();
        provider.SetUtcNow(DateTimeOffset.UtcNow);
        transportSettings = new TransportSettings
        {
            ConnectionString = configuration.ConnectionString,
            MaxConcurrency = 1,
            EndpointName = Guid.NewGuid().ToString("N")
        };
        query = new AmazonSQSQuery(NullLogger<AmazonSQSQuery>.Instance, provider);
    }

    [Test]
    public async Task TestConnectionWithInvalidAccessKeySettings()
    {
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var dictionary = new Dictionary<string, string>
        {
            { AmazonSQSQuery.AmazonSQSSettings.AccessKey, "not_valid" },
            { AmazonSQSQuery.AmazonSQSSettings.SecretKey, "not_valid" },
            { AmazonSQSQuery.AmazonSQSSettings.Region, "us-east-1" }
        };
        query.Initialise(dictionary.ToFrozenDictionary());
        (bool success, List<string> errors, string diagnostics) =
            await query.TestConnection(cancellationTokenSource.Token);

        Assert.IsFalse(success);
        Assert.AreEqual("The security token included in the request is invalid.", errors.Single());
        Approver.Verify(diagnostics);
    }

    [Test]
    public async Task TestConnectionWithInvalidRegionSettings()
    {
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var dictionary = new Dictionary<string, string>
        {
            { AmazonSQSQuery.AmazonSQSSettings.Region, "not_valid" }
        };
        query.Initialise(dictionary.ToFrozenDictionary());
        (bool success, List<string> errors, string diagnostics) =
            await query.TestConnection(cancellationTokenSource.Token);

        Assert.IsFalse(success);
        Assert.AreEqual("Invalid region endpoint provided", errors.Single());
        Approver.Verify(diagnostics);
    }

    [Test]
    public async Task TestConnectionWithValidSettings()
    {
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(332240));

        var dictionary = new Dictionary<string, string>();
        query.Initialise(dictionary.ToFrozenDictionary());
        (bool success, _, string diagnostics) = await query.TestConnection(cancellationTokenSource.Token);

        Approver.Verify(diagnostics);
        Assert.IsTrue(success);
    }

    [Test]
    public async Task RunScenario()
    {
        // We need to wait a bit of time, to ensure AWS metrics are retrievable
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(6));
        CancellationToken token = cancellationTokenSource.Token;
        const int messagesSent = 15;
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

        var connectionString =
            new SQSTransportConnectionString(transportSettings.ConnectionString);
        var dictionary = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(connectionString.AccessKey))
        {
            dictionary.Add(AmazonSQSQuery.AmazonSQSSettings.AccessKey, connectionString.AccessKey);
        }
        if (!string.IsNullOrEmpty(connectionString.SecretKey))
        {
            dictionary.Add(AmazonSQSQuery.AmazonSQSSettings.SecretKey, connectionString.SecretKey);
        }
        if (!string.IsNullOrEmpty(connectionString.Region))
        {
            dictionary.Add(AmazonSQSQuery.AmazonSQSSettings.Region, connectionString.Region);
        }

        query.Initialise(dictionary.ToFrozenDictionary());

        await Task.Delay(TimeSpan.FromMinutes(2));

        var queueNames = new List<IBrokerQueue>();
        await foreach (IBrokerQueue queueName in query.GetQueueNames(token))
        {
            queueNames.Add(queueName);
        }

        IBrokerQueue queue = queueNames.Find(name => name.QueueName == $"{connectionString.QueueNamePrefix}{transportSettings.EndpointName}");
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