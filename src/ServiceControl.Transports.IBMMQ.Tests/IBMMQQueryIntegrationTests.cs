namespace ServiceControl.Transport.Tests;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IBM.WMQ;
using IBM.WMQ.PCF;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NUnit.Framework;
using Transports;
using Transports.IBMMQ;
using Transports.BrokerThroughput;

[TestFixture]
[Category("IntegrationTests")]
class IBMMQQueryIntegrationTests
{
    const int StatisticsIntervalSeconds = 10;
    const int StatisticsWaitSeconds = 25;
    static readonly string TestQueueName = "SC.TEST.THROUGHPUT.Q";
    static readonly string ForwardingQueueName = "SC.TEST.STATS.FWD";

    [OneTimeSetUp]
    public async Task EnableStatistics()
    {
        var (qmName, props) = ConnectionProperties.Parse(ConnectionString);
        using var qm = new MQQueueManager(qmName, props);

        // Enable queue-manager-wide statistics with a short interval so the integration
        // test can validate stats events without waiting 30+ minutes (the broker default).
        ChangeQueueManager(qm, MQC.MQIA_STATISTICS_Q, MQC.MQMON_ON);
        ChangeQueueManager(qm, MQC.MQIA_STATISTICS_INTERVAL, StatisticsIntervalSeconds);

        EnsureQueue(qm, TestQueueName);
        EnsureQueue(qm, ForwardingQueueName);

        // Drain anything left from prior runs so each test starts from a known empty state.
        DrainQueue(qm, "SYSTEM.ADMIN.STATISTICS.QUEUE");
        DrainQueue(qm, ForwardingQueueName);
        DrainQueue(qm, TestQueueName);

        await Task.CompletedTask.ConfigureAwait(false);
    }

    [Test]
    public async Task GetThroughputPerDay_returns_dequeue_counts_from_statistics_messages()
    {
        const int messagesToPut = 5;
        var (qmName, props) = ConnectionProperties.Parse(ConnectionString);
        using (var qm = new MQQueueManager(qmName, props))
        {
            DrainQueue(qm, "SYSTEM.ADMIN.STATISTICS.QUEUE");
            PutAndGetMessages(qm, TestQueueName, messagesToPut);
        }

        // Wait for the broker to emit a statistics interval covering the activity above.
        await Task.Delay(TimeSpan.FromSeconds(StatisticsWaitSeconds)).ConfigureAwait(false);

        var query = CreateQuery();
        query.Initialize(EmptySettings());
        Assume.That(query.HasInitialisationErrors(out var initError), Is.False, initError);

        var queues = new List<IBrokerQueue>();
        await foreach (var q in query.GetQueueNames(default).ConfigureAwait(false))
        {
            queues.Add(q);
        }

        var testQueue = queues.FirstOrDefault(q => q.QueueName == TestQueueName);
        Assert.That(testQueue, Is.Not.Null, $"GetQueueNames did not enumerate {TestQueueName}");

        var rows = new List<QueueThroughput>();
        await foreach (var row in query.GetThroughputPerDay(testQueue!, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)), default).ConfigureAwait(false))
        {
            rows.Add(row);
        }

        Assert.That(rows, Is.Not.Empty, $"No throughput rows returned for {TestQueueName}");
        var total = rows.Sum(r => r.TotalThroughput);
        Assert.That(total, Is.GreaterThanOrEqualTo(messagesToPut),
            $"Expected at least {messagesToPut} dequeues; got {total}. Rows: {string.Join(", ", rows.Select(r => $"{r.DateUTC}={r.TotalThroughput}"))}");
    }

    [Test]
    public async Task Forwarding_publishes_a_copy_of_each_consumed_statistics_message()
    {
        const int messagesToPut = 3;
        var (qmName, props) = ConnectionProperties.Parse(ConnectionString);
        using (var qm = new MQQueueManager(qmName, props))
        {
            DrainQueue(qm, "SYSTEM.ADMIN.STATISTICS.QUEUE");
            DrainQueue(qm, ForwardingQueueName);
            PutAndGetMessages(qm, TestQueueName, messagesToPut);
        }

        await Task.Delay(TimeSpan.FromSeconds(StatisticsWaitSeconds)).ConfigureAwait(false);

        var query = CreateQuery();
        query.Initialize(new ReadOnlyDictionary<string, string>(new Dictionary<string, string>
        {
            ["IBMMQ/StatisticsForwardingQueue"] = ForwardingQueueName
        }));
        Assume.That(query.HasInitialisationErrors(out var initError), Is.False, initError);

        await foreach (var _ in query.GetQueueNames(default).ConfigureAwait(false))
        {
            // drain enumeration so the cache is populated as part of GetQueueNames
        }

        // After draining, the forwarding queue should now contain a copy of each PCF message.
        int forwardedCount;
        using (var qm = new MQQueueManager(qmName, props))
        {
            forwardedCount = CountMessagesViaBrowse(qm, ForwardingQueueName);
        }

        Assert.That(forwardedCount, Is.GreaterThan(0),
            $"Expected at least one forwarded statistics message on {ForwardingQueueName} but found {forwardedCount}.");
    }

    [Test]
    public async Task TestConnection_reports_actionable_error_when_statistics_disabled()
    {
        var (qmName, props) = ConnectionProperties.Parse(ConnectionString);
        using (var qm = new MQQueueManager(qmName, props))
        {
            ChangeQueueManager(qm, MQC.MQIA_STATISTICS_Q, MQC.MQMON_OFF);
        }

        try
        {
            var query = CreateQuery();
            query.Initialize(EmptySettings());
            var (success, errors, diagnostics) = await query.TestConnection(default).ConfigureAwait(false);

            Assert.That(success, Is.False);
            Assert.That(string.Join("\n", errors), Does.Contain("STATQ=OFF"));
            Assert.That(diagnostics, Does.Contain("ALTER QMGR STATQ(ON)"));
        }
        finally
        {
            using var qm = new MQQueueManager(qmName, props);
            ChangeQueueManager(qm, MQC.MQIA_STATISTICS_Q, MQC.MQMON_ON);
        }
    }

    static IBMMQQuery CreateQuery() =>
        new(NullLogger<IBMMQQuery>.Instance, new FakeTimeProvider(DateTimeOffset.UtcNow),
            new TransportSettings { ConnectionString = ConnectionString });

    static ReadOnlyDictionary<string, string> EmptySettings() =>
        new(new Dictionary<string, string>());

    static void ChangeQueueManager(MQQueueManager qm, int parameter, int value)
    {
        var agent = new PCFMessageAgent(qm);
        try
        {
            var request = new PCFMessage(MQC.MQCMD_CHANGE_Q_MGR);
            request.AddParameter(parameter, value);
            agent.Send(request);
        }
        finally
        {
            agent.Disconnect();
        }
    }

    static void EnsureQueue(MQQueueManager qm, string queueName)
    {
        var agent = new PCFMessageAgent(qm);
        try
        {
            try
            {
                var inquire = new PCFMessage(MQC.MQCMD_INQUIRE_Q);
                inquire.AddParameter(MQC.MQCA_Q_NAME, queueName);
                agent.Send(inquire);
                return; // exists
            }
            catch (PCFException ex) when (ex.ReasonCode == MQC.MQRC_UNKNOWN_OBJECT_NAME)
            {
                // create
            }

            var create = new PCFMessage(MQC.MQCMD_CREATE_Q);
            create.AddParameter(MQC.MQCA_Q_NAME, queueName);
            create.AddParameter(MQC.MQIA_Q_TYPE, MQC.MQQT_LOCAL);
            agent.Send(create);
        }
        finally
        {
            agent.Disconnect();
        }
    }

    static void PutAndGetMessages(MQQueueManager qm, string queueName, int count)
    {
        using var queue = qm.AccessQueue(queueName,
            MQC.MQOO_OUTPUT | MQC.MQOO_INPUT_SHARED | MQC.MQOO_FAIL_IF_QUIESCING);

        for (var i = 0; i < count; i++)
        {
            var msg = new MQMessage();
            msg.WriteString($"throughput-test-{i}");
            queue.Put(msg);
        }

        var gmo = new MQGetMessageOptions { Options = MQC.MQGMO_NO_WAIT, WaitInterval = 0 };
        for (var i = 0; i < count; i++)
        {
            queue.Get(new MQMessage(), gmo);
        }
    }

    static void DrainQueue(MQQueueManager qm, string queueName)
    {
        try
        {
            using var queue = qm.AccessQueue(queueName,
                MQC.MQOO_INPUT_SHARED | MQC.MQOO_FAIL_IF_QUIESCING);
            var gmo = new MQGetMessageOptions { Options = MQC.MQGMO_NO_WAIT, WaitInterval = 0 };
            while (true)
            {
                try
                {
                    queue.Get(new MQMessage(), gmo);
                }
                catch (MQException e) when (e.ReasonCode == MQC.MQRC_NO_MSG_AVAILABLE)
                {
                    break;
                }
            }
        }
        catch (MQException e) when (e.ReasonCode == MQC.MQRC_UNKNOWN_OBJECT_NAME)
        {
            // queue does not exist yet; nothing to drain
        }
    }

    static int CountMessagesViaBrowse(MQQueueManager qm, string queueName)
    {
        using var queue = qm.AccessQueue(queueName,
            MQC.MQOO_BROWSE | MQC.MQOO_FAIL_IF_QUIESCING);
        var count = 0;
        var gmo = new MQGetMessageOptions
        {
            Options = MQC.MQGMO_BROWSE_FIRST | MQC.MQGMO_NO_WAIT,
            WaitInterval = 0
        };
        while (true)
        {
            try
            {
                queue.Get(new MQMessage(), gmo);
                count++;
                gmo.Options = MQC.MQGMO_BROWSE_NEXT | MQC.MQGMO_NO_WAIT;
            }
            catch (MQException e) when (e.ReasonCode == MQC.MQRC_NO_MSG_AVAILABLE)
            {
                break;
            }
        }
        return count;
    }

    static readonly string ConnectionString =
        Environment.GetEnvironmentVariable("ServiceControl_TransportTests_IBMMQ_ConnectionString")
        ?? Environment.GetEnvironmentVariable("SERVICECONTROL_TRANSPORTTESTS_IBMMQ_CONNECTIONSTRING")
        ?? "mq://admin:passw0rd@localhost:1414/QM1?channel=DEV.ADMIN.SVRCONN";
}
