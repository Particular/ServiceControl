namespace ServiceControl.Transport.Tests;

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NUnit.Framework;
using Particular.Approvals;
using Transports;
using Transports.SqlServer;

[TestFixture]
class SqlServerQueryTests : TransportTestFixture
{
    FakeTimeProvider provider;
    TransportSettings transportSettings;
    SqlServerQuery query;

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
        query = new SqlServerQuery(NullLogger<SqlServerQuery>.Instance, provider, transportSettings);
    }

    [Test]
    public async Task TestConnectionWithInvalidConnectionStringSettings()
    {
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var dictionary = new Dictionary<string, string>
        {
            { SqlServerQuery.SqlServerSettings.ConnectionString, "not valid" }
        };
        query.Initialise(dictionary.ToFrozenDictionary());
        (bool success, List<string> errors, string diagnostics) =
            await query.TestConnection(cancellationTokenSource.Token);

        Assert.IsFalse(success);
        Assert.AreEqual("SQL Connection String could not be parsed.", errors.Single());
        Approver.Verify(diagnostics);
    }

    [Test]
    public async Task TestConnectionWithInvalidCatalogSettings()
    {
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var dictionary = new Dictionary<string, string>
        {
            { SqlServerQuery.SqlServerSettings.ConnectionString, configuration.ConnectionString },
            { SqlServerQuery.SqlServerSettings.AdditionalCatalogs, "not_here" }
        };
        query.Initialise(dictionary.ToFrozenDictionary());
        (bool success, List<string> errors, string diagnostics) =
            await query.TestConnection(cancellationTokenSource.Token);

        Assert.IsFalse(success);
        StringAssert.StartsWith("Cannot open database \"not_here\"", errors.Single());
        Approver.Verify(diagnostics,
            s => Regex.Replace(s, "^Login failed for user .*$", "Login failed for user.", RegexOptions.Multiline));
    }

    [Test]
    public async Task TestConnectionWithValidSettings()
    {
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var dictionary = new Dictionary<string, string>
        {
            { SqlServerQuery.SqlServerSettings.ConnectionString, configuration.ConnectionString }
        };
        query.Initialise(dictionary.ToFrozenDictionary());
        (bool success, _, string diagnostics) = await query.TestConnection(cancellationTokenSource.Token);

        Assert.IsTrue(success);
        Approver.Verify(diagnostics);
    }

    [Test]
    public async Task RunScenario()
    {
        // We need to wait a bit of time, because the scenario running takes on average 1 sec per run.
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(50));
        CancellationToken token = cancellationTokenSource.Token;
        var dictionary = new Dictionary<string, string>
        {
            { SqlServerQuery.SqlServerSettings.ConnectionString, configuration.ConnectionString }
        };

        await CreateTestQueue(transportSettings.EndpointName);

        query.Initialise(dictionary.ToFrozenDictionary());

        var queueNames = new List<IBrokerQueue>();
        await foreach (IBrokerQueue queueName in query.GetQueueNames(token))
        {
            queueNames.Add(queueName);
        }

        IBrokerQueue queue = queueNames.Find(name => ((BrokerQueueTable)name).Name == transportSettings.EndpointName);
        Assert.IsNotNull(queue);

        long total = 0L;
        using var reset = new ManualResetEventSlim();

        var runScenarioAndAdvanceTime = Task.Run(async () =>
        {
            while (!reset.IsSet)
            {
                await SendAndReceiveMessages(transportSettings.EndpointName, 1);
                provider.Advance(TimeSpan.FromHours(1));
            }
        }, token);

        await foreach (QueueThroughput queueThroughput in query.GetThroughputPerDay(queue, new DateOnly(), token))
        {
            total += queueThroughput.TotalThroughput;
        }

        reset.Set();
        await runScenarioAndAdvanceTime.WaitAsync(token);

        // Asserting that we have one message per hour during 24 hours, the first snapshot is not counted hence the 23 assertion. 
        Assert.AreEqual(23, total);
    }
}