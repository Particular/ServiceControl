namespace ServiceControl.Transport.Tests;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Npgsql;
using NUnit.Framework;
using Particular.Approvals;
using Transports;
using Transports.PostgreSql;
using Transports.BrokerThroughput;

[TestFixture]
class PostgreSqlQueryTests : TransportTestFixture
{
    FakeTimeProvider provider;
    TransportSettings transportSettings;
    PostgreSqlQuery query;

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
        query = new PostgreSqlQuery(NullLogger<PostgreSqlQuery>.Instance, provider, transportSettings);
    }

    [Test]
    public async Task TestConnectionWithInvalidConnectionStringSettings()
    {
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var dictionary = new Dictionary<string, string>
        {
            { PostgreSqlQuery.PostgreSqlSettings.ConnectionString, "not valid" }
        };
        query.Initialize(new ReadOnlyDictionary<string, string>(dictionary));
        (bool success, List<string> errors, string diagnostics) =
            await query.TestConnection(cancellationTokenSource.Token);

        Assert.That(success, Is.False);
        Assert.That(errors.Single(), Is.EqualTo("PostgreSQL Connection String could not be parsed."));
        Approver.Verify(diagnostics);
    }

    [Test]
    public async Task TestConnectionWithValidSettings()
    {
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var dictionary = new Dictionary<string, string>
        {
            { PostgreSqlQuery.PostgreSqlSettings.ConnectionString, configuration.ConnectionString }
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
            { PostgreSqlQuery.PostgreSqlSettings.ConnectionString, configuration.ConnectionString }
        };

        await CreateTestQueue(transportSettings.EndpointName);

        query.Initialize(new ReadOnlyDictionary<string, string>(dictionary));

        var queueNames = new List<IBrokerQueue>();
        await foreach (IBrokerQueue queueName in query.GetQueueNames(token))
        {
            queueNames.Add(queueName);
        }

        IBrokerQueue queue = queueNames.Find(name => ((BrokerQueueTable)name).SanitizedName == transportSettings.EndpointName);
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

        // The sequence already reports a last_value of 1 before the first message is sent, so that
        // message is not visible in any of the hourly deltas, hence the 23 instead of 24.
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

    [Test]
    public async Task NoNegativeThroughputWhenQueueTableIsDeletedBetweenSnapshots()
    {
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(50));
        CancellationToken token = cancellationTokenSource.Token;
        var dictionary = new Dictionary<string, string>
        {
            { PostgreSqlQuery.PostgreSqlSettings.ConnectionString, configuration.ConnectionString }
        };

        await CreateTestQueue(transportSettings.EndpointName);

        query.Initialize(new ReadOnlyDictionary<string, string>(dictionary));

        var queueNames = new List<IBrokerQueue>();
        await foreach (IBrokerQueue queueName in query.GetQueueNames(token))
        {
            queueNames.Add(queueName);
        }

        IBrokerQueue queue = queueNames.Find(name => ((BrokerQueueTable)name).SanitizedName == transportSettings.EndpointName);
        Assert.That(queue, Is.Not.Null);

        // Send messages so the start snapshot has a positive RowVersion
        await SendAndReceiveMessages(transportSettings.EndpointName, 5);

        var brokerQueueTable = (BrokerQueueTable)queue;
        int advanceCount = 0;
        using var done = new ManualResetEventSlim();

        var dropTableTask = Task.Run(async () =>
        {
            while (!done.IsSet)
            {
                // Drop the table mid-way while GetThroughputPerDay is waiting for the next hour
                if (advanceCount == 3)
                {
                    await DropQueueTable(brokerQueueTable.QueueAddress.QualifiedTableName, token);
                }

                provider.Advance(TimeSpan.FromHours(1));
                advanceCount++;

                // Pace advances to give GetThroughputPerDay time to process the iteration
                // and register its next Task.Delay before we advance again
                await Task.Delay(TimeSpan.FromMilliseconds(500), CancellationToken.None);
            }
        }, token);

        var throughputValues = new List<QueueThroughput>();
        await foreach (QueueThroughput queueThroughput in query.GetThroughputPerDay(queue, new DateOnly(), token))
        {
            throughputValues.Add(queueThroughput);
        }

        done.Set();
        await dropTableTask.WaitAsync(token);

        Assert.That(throughputValues, Has.All.Matches<QueueThroughput>(qt => qt.TotalThroughput >= 0));
    }

    async Task DropQueueTable(string qualifiedTableName, CancellationToken cancellationToken)
    {
        await using var conn = new NpgsqlConnection(configuration.ConnectionString);
        await conn.OpenAsync(cancellationToken);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"DROP TABLE IF EXISTS {qualifiedTableName} CASCADE;";
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }
}