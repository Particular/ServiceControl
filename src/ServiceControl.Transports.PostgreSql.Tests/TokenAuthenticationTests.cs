namespace ServiceControl.Transport.Tests;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using NServiceBus.Transport;
using NUnit.Framework;
using ServiceControl.PostgreSql.TokenAuthentication;
using Transports;
using Transports.BrokerThroughput;
using Transports.PostgreSql;

[TestFixture]
class TokenAuthenticationTests : TransportTestFixture
{
    CountingTokenProvider tokenProvider;
    string username;

    public override async Task Setup()
    {
        await base.Setup();

        var builder = new NpgsqlConnectionStringBuilder(configuration.ConnectionString);
        tokenProvider = new CountingTokenProvider(builder.Password);
        username = builder.Username;
        builder.Remove("Password");

        configuration.UseTokenAuthentication($"{builder.ConnectionString};Token Authentication=AwsRdsIam", tokenProvider);
    }

    [Test]
    public async Task Provisions_sends_and_receives_with_one_token()
    {
        var queueName = GetTestQueueName("token");
        await CreateTestQueue(queueName);
        var received = CreateTaskCompletionSource<bool>();

        await StartQueueIngestor(
            queueName,
            (_, __) =>
            {
                received.TrySetResult(true);
                return Task.CompletedTask;
            },
            (_, __) =>
            {
                Assert.Fail("There should be no errors");
                return Task.FromResult(ErrorHandleResult.Handled);
            });

        var dispatcher = await configuration.TransportCustomization.CreateTransportInfrastructure("TokenDispatcher", Settings("TokenDispatcher"));

        try
        {
            await dispatcher.Dispatcher.SendTestMessage(queueName, "some content", configuration.TransportCustomization);

            Assert.That(await received.Task, Is.True);
        }
        finally
        {
            await dispatcher.Shutdown();
        }

        using (Assert.EnterMultipleScope())
        {
            Assert.That(tokenProvider.Calls, Is.EqualTo(1));
            Assert.That(tokenProvider.Username, Is.EqualTo(username));
        }
    }

    [Test]
    public async Task Reports_queue_length_with_a_token()
    {
        var queueName = GetTestQueueName("tokenlength");
        await CreateTestQueue(queueName);
        var reported = CreateTaskCompletionSource<QueueLengthEntry>();

        await using var scope = await StartQueueLengthProvider(queueName, entry =>
        {
            if (entry.Value > 0)
            {
                reported.TrySetResult(entry);
            }
        });

        await Dispatcher.SendTestMessage(queueName, "some content", configuration.TransportCustomization);

        using (Assert.EnterMultipleScope())
        {
            Assert.That((await reported.Task).Value, Is.EqualTo(1));
            Assert.That(tokenProvider.Calls, Is.EqualTo(1));
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Queries_throughput_with_a_token(bool setThroughputConnectionString)
    {
        var queueName = GetTestQueueName("tokenthroughput");
        await CreateTestQueue(queueName);
        var query = CreateThroughputQuery(queueName);
        var settings = new Dictionary<string, string>();

        if (setThroughputConnectionString)
        {
            settings[PostgreSqlQuery.PostgreSqlSettings.ConnectionString] = configuration.ConnectionString;
        }

        query.Initialize(new ReadOnlyDictionary<string, string>(settings));
        var (success, errors, _) = await query.TestConnection();
        var queueNames = new List<string>();

        await foreach (var queue in query.GetQueueNames())
        {
            queueNames.Add(queue.SanitizedName);
        }

        using (Assert.EnterMultipleScope())
        {
            Assert.That(success, Is.True, string.Join(Environment.NewLine, errors));
            Assert.That(queueNames, Does.Contain(queueName));
            Assert.That(tokenProvider.Calls, Is.EqualTo(1));
        }
    }

    [Test]
    public async Task Names_the_throughput_setting_when_its_token_keywords_are_invalid()
    {
        var query = CreateThroughputQuery(GetTestQueueName("tokeninvalid"));

        query.Initialize(new ReadOnlyDictionary<string, string>(new Dictionary<string, string>
        {
            [PostgreSqlQuery.PostgreSqlSettings.ConnectionString] = "Host=localhost;Token Authentication=AwsRdsIam"
        }));
        var (success, errors, _) = await query.TestConnection();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(success, Is.False);
            Assert.That(errors, Has.One.StartsWith("LicensingComponent/PostgreSQL/ConnectionString is invalid.").And.Contain("Username"));
        }
    }

    [Test]
    public void Names_the_transport_connection_string_when_its_token_keywords_are_invalid()
    {
        var settings = Settings(GetTestQueueName("tokeninvalid"));
        settings.ConnectionString = "Host=localhost;Username=sc;Token Authentication=Kerberos";

        var exception = Assert.ThrowsAsync<InvalidOperationException>(() => configuration.TransportCustomization.ProvisionQueues(settings, []));

        Assert.That(exception.Message, Does.StartWith("The transport connection string is invalid.").And.Contain("AwsRdsIam"));
    }

    IBrokerThroughputQuery CreateThroughputQuery(string endpointName)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        configuration.TransportCustomization.AddTransportForPrimary(services, Settings(endpointName));

        return services.BuildServiceProvider().GetRequiredService<IBrokerThroughputQuery>();
    }

    TransportSettings Settings(string endpointName) => new()
    {
        ConnectionString = configuration.ConnectionString,
        EndpointName = endpointName,
        MaxConcurrency = 1
    };

    sealed class CountingTokenProvider(string password) : IPostgreSqlTokenProvider
    {
        int calls;

        public int Calls => Volatile.Read(ref calls);

        public string Username { get; private set; }

        public ValueTask<string> GetToken(NpgsqlConnectionStringBuilder settings, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref calls);
            Username = settings.Username;

            return ValueTask.FromResult(password);
        }
    }
}
