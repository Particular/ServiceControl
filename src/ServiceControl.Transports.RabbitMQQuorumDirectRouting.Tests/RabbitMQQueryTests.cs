namespace ServiceControl.Transport.Tests;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NUnit.Framework;
using Particular.Approvals;
using ServiceControl.Transports.BrokerThroughput;
using Transports;
using Transports.RabbitMQ;

[TestFixture]
class RabbitMQQueryTests : TransportTestFixture
{
    FakeTimeProvider provider;
    TransportSettings transportSettings;
    RabbitMQQuery query;

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
        query = new RabbitMQQuery(NullLogger<RabbitMQQuery>.Instance, provider, transportSettings);
    }

    [Test]
    public async Task TestConnectionWithInvalidSettings()
    {
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var dictionary = new Dictionary<string, string>
        {
            { RabbitMQQuery.RabbitMQSettings.API, "http://localhost:12345" }
        };
        query.Initialize(new ReadOnlyDictionary<string, string>(dictionary));
        (bool success, _, string diagnostics) = await query.TestConnection(cancellationTokenSource.Token);

        Assert.That(success, Is.False);
        Approver.Verify(diagnostics,
            s => Regex.Replace(s, "defaulted to using \"\\w*\" username", "defaulted to using \"xxxxx\" username",
                RegexOptions.Multiline));
    }

    [Test]
    public async Task TestConnectionWithValidSettings()
    {
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        query.Initialize(ReadOnlyDictionary<string, string>.Empty);
        (bool success, _, string diagnostics) = await query.TestConnection(cancellationTokenSource.Token);

        Assert.That(success, Is.True);
        Approver.Verify(diagnostics,
            s =>
            {
                s = Regex.Replace(s,
                    "RabbitMQ API Url not set, defaulted to using \"http://[\\w.]*:15672\" from the ConnectionString used by instance",
                    "RabbitMQ API Url not set, defaulted to using \"xxxx\" from the ConnectionString used by instance",
                    RegexOptions.Multiline);
                return Regex.Replace(s, "defaulted to using \"\\w*\" username", "defaulted to using \"xxxxx\" username",
                    RegexOptions.Multiline);
            });
    }

    [Test]
    public async Task RunScenario()
    {
        // We need to wait a bit of time, because the scenario running takes on average 1 sec per run.
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        CancellationToken token = cancellationTokenSource.Token;

        await CreateTestQueue(transportSettings.EndpointName);

        query.Initialize(ReadOnlyDictionary<string, string>.Empty);

        var queueNames = new List<IBrokerQueue>();
        await foreach (IBrokerQueue queueName in query.GetQueueNames(token))
        {
            queueNames.Add(queueName);
        }

        IBrokerQueue queue = queueNames.Find(name => name.QueueName == transportSettings.EndpointName);
        Assert.That(queue, Is.Not.Null);

        long total = 0L;
        const int numMessagesToIngest = 15;
        using var reset = new ManualResetEventSlim();
        using var sendMessage = new ManualResetEventSlim();

        var runScenarioAndAdvanceTime = Task.Run(async () =>
        {
            await Task.Delay(100, token);
            provider.Advance(TimeSpan.FromMinutes(15));

            sendMessage.Wait(token);

            while (!reset.IsSet)
            {
                await SendAndReceiveMessages(transportSettings.EndpointName, numMessagesToIngest);
                await Task.Delay(100, token);
                provider.Advance(TimeSpan.FromMinutes(15));
            }
        }, token);

        await foreach (QueueThroughput queueThroughput in query.GetThroughputPerDay(queue, new DateOnly(), token))
        {
            sendMessage.Set();
            total += queueThroughput.TotalThroughput;
        }

        reset.Set();
        await runScenarioAndAdvanceTime.WaitAsync(token);

        // Asserting that we have one message per hour during 24 hours, the first snapshot is not counted hence the 23 assertion. 
        Assert.That(total, Is.GreaterThan(numMessagesToIngest));
    }
}