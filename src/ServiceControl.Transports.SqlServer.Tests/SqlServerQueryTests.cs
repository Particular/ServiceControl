namespace ServiceControl.Transport.Tests;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
using ServiceControl.Transports.BrokerThroughput;

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
        query.Initialize(new ReadOnlyDictionary<string, string>(dictionary));
        (bool success, List<string> errors, string diagnostics) =
            await query.TestConnection(cancellationTokenSource.Token);

        Assert.That(success, Is.False);
        Assert.That(errors.Single(), Is.EqualTo("SQL Connection String could not be parsed."));
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
        query.Initialize(new ReadOnlyDictionary<string, string>(dictionary));
        (bool success, List<string> errors, string diagnostics) =
            await query.TestConnection(cancellationTokenSource.Token);

        Assert.That(success, Is.False);
        Assert.That(errors.Single(), Does.StartWith("Could not connect to 'not_here'"));
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
        query.Initialize(new ReadOnlyDictionary<string, string>(dictionary));
        (bool success, _, string diagnostics) = await query.TestConnection(cancellationTokenSource.Token);

        Assert.That(success, Is.True);
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

        query.Initialize(new ReadOnlyDictionary<string, string>(dictionary));

        var queueNames = new List<IBrokerQueue>();
        await foreach (IBrokerQueue queueName in query.GetQueueNames(token))
        {
            queueNames.Add(queueName);
        }

        IBrokerQueue queue = queueNames.Find(name => ((BrokerQueueTable)name).Name == transportSettings.EndpointName);
        Assert.That(queue, Is.Not.Null);

        long total = 0L;

        await using var throughputPerDay = query.GetThroughputPerDay(queue, new DateOnly(), token).GetAsyncEnumerator(token);

        // Sending the message before asking for the next hourly value keeps every message inside
        // exactly one snapshot interval, instead of racing the snapshots from a background task.
        for (int hour = 0; hour < 24; hour++)
        {
            await SendAndReceiveMessages(transportSettings.EndpointName, 1);

            Assert.That(await MoveToNextHour(throughputPerDay, token), Is.True);

            total += throughputPerDay.Current.TotalThroughput;
        }

        // IDENT_CURRENT already reports 1 before the first message is sent, so that message is not
        // visible in any of the hourly deltas, hence the 23 instead of 24.
        Assert.That(total, Is.EqualTo(23));
    }

    // The query only takes its next snapshot once an hour has passed on the fake clock, but it
    // registers that timer asynchronously after the previous value was consumed, so an advance can
    // land before the timer exists. Keep advancing until the value arrives.
    async Task<bool> MoveToNextHour(IAsyncEnumerator<QueueThroughput> throughputPerDay, CancellationToken cancellationToken)
    {
        var moveNext = throughputPerDay.MoveNextAsync().AsTask();

        while (!moveNext.IsCompleted)
        {
            provider.Advance(TimeSpan.FromHours(1));

            await Task.WhenAny(moveNext, Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken));
        }

        return await moveNext;
    }
}