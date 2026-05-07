namespace ServiceControl.Transport.Tests;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NUnit.Framework;
using Transports;
using Transports.IBMMQ;

[TestFixture]
class IBMMQQueryTests
{
    [Test]
    public void Settings_lists_three_keys()
    {
        var query = new IBMMQQuery(NullLogger<IBMMQQuery>.Instance, new FakeTimeProvider(), new TransportSettings());
        Assert.That(query.Settings, Has.Length.EqualTo(3));
        Assert.That(query.Settings[0].Key, Is.EqualTo("IBMMQ/ConnectionString"));
        Assert.That(query.Settings[1].Key, Is.EqualTo("IBMMQ/StatisticsQueue"));
        Assert.That(query.Settings[2].Key, Is.EqualTo("IBMMQ/StatisticsForwardingQueue"));
    }

    [Test]
    public void MessageTransport_is_IBMMQ()
    {
        var query = new IBMMQQuery(NullLogger<IBMMQQuery>.Instance, new FakeTimeProvider(), new TransportSettings());
        Assert.That(query.MessageTransport, Is.EqualTo("IBMMQ"));
    }

    [Test]
    public void Initialize_with_no_connection_string_records_actionable_error()
    {
        var query = new IBMMQQuery(NullLogger<IBMMQQuery>.Instance, new FakeTimeProvider(), new TransportSettings());

        query.Initialize(new ReadOnlyDictionary<string, string>(new Dictionary<string, string>()));

        Assert.That(query.HasInitialisationErrors(out var error), Is.True);
        Assert.That(error, Does.Contain("connection string"));
    }

    [Test]
    public void Initialize_uses_setting_override_when_present()
    {
        var transportSettings = new TransportSettings { ConnectionString = "mq://transport-default-host:1414/QM1" };
        var query = new IBMMQQuery(NullLogger<IBMMQQuery>.Instance, new FakeTimeProvider(), transportSettings);

        var settings = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>
        {
            ["IBMMQ/ConnectionString"] = "mq://override-host:9999/QM2"
        });

        // Initialize attempts a real connection; we don't care if it fails — we care that the override
        // is recorded in Diagnostics so operators can see which connection string was used.
        query.Initialize(settings);

        var (_, _, diagnostics) = query.TestConnection(default).GetAwaiter().GetResult();
        Assert.That(diagnostics, Does.Contain("ConnectionString set via 'IBMMQ/ConnectionString'"));
    }

    [Test]
    public void Initialize_diagnostics_record_default_connection_source()
    {
        var transportSettings = new TransportSettings { ConnectionString = "mq://transport-default-host:1414/QM1" };
        var query = new IBMMQQuery(NullLogger<IBMMQQuery>.Instance, new FakeTimeProvider(), transportSettings);

        query.Initialize(new ReadOnlyDictionary<string, string>(new Dictionary<string, string>()));

        var (_, _, diagnostics) = query.TestConnection(default).GetAwaiter().GetResult();
        Assert.That(diagnostics, Does.Contain("ConnectionString defaulted to the Primary instance's transport connection string"));
    }

    [Test]
    public void Initialize_records_statistics_queue_setting()
    {
        var transportSettings = new TransportSettings { ConnectionString = "mq://localhost:1414/QM1" };
        var query = new IBMMQQuery(NullLogger<IBMMQQuery>.Instance, new FakeTimeProvider(), transportSettings);

        query.Initialize(new ReadOnlyDictionary<string, string>(new Dictionary<string, string>
        {
            ["IBMMQ/StatisticsQueue"] = "MY.CUSTOM.STATS.QUEUE"
        }));

        var (_, _, diagnostics) = query.TestConnection(default).GetAwaiter().GetResult();
        Assert.That(diagnostics, Does.Contain("Statistics queue: MY.CUSTOM.STATS.QUEUE"));
    }

    [Test]
    public void Initialize_records_default_statistics_queue_when_not_set()
    {
        var transportSettings = new TransportSettings { ConnectionString = "mq://localhost:1414/QM1" };
        var query = new IBMMQQuery(NullLogger<IBMMQQuery>.Instance, new FakeTimeProvider(), transportSettings);

        query.Initialize(new ReadOnlyDictionary<string, string>(new Dictionary<string, string>()));

        var (_, _, diagnostics) = query.TestConnection(default).GetAwaiter().GetResult();
        Assert.That(diagnostics, Does.Contain("Statistics queue: SYSTEM.ADMIN.STATISTICS.QUEUE"));
    }

    [Test]
    public void Initialize_records_forwarding_queue_when_set()
    {
        var transportSettings = new TransportSettings { ConnectionString = "mq://localhost:1414/QM1" };
        var query = new IBMMQQuery(NullLogger<IBMMQQuery>.Instance, new FakeTimeProvider(), transportSettings);

        query.Initialize(new ReadOnlyDictionary<string, string>(new Dictionary<string, string>
        {
            ["IBMMQ/StatisticsForwardingQueue"] = "MY.FORWARD.QUEUE"
        }));

        var (_, _, diagnostics) = query.TestConnection(default).GetAwaiter().GetResult();
        Assert.That(diagnostics, Does.Contain("Statistics forwarding queue: MY.FORWARD.QUEUE"));
    }

    [Test]
    public void Initialize_records_no_forwarding_when_unset()
    {
        var transportSettings = new TransportSettings { ConnectionString = "mq://localhost:1414/QM1" };
        var query = new IBMMQQuery(NullLogger<IBMMQQuery>.Instance, new FakeTimeProvider(), transportSettings);

        query.Initialize(new ReadOnlyDictionary<string, string>(new Dictionary<string, string>()));

        var (_, _, diagnostics) = query.TestConnection(default).GetAwaiter().GetResult();
        Assert.That(diagnostics, Does.Contain("Statistics forwarding queue: (not configured)"));
    }
}
