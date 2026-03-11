namespace ServiceControl.Transport.Tests;

using System;
using System.Collections;
using System.Threading.Tasks;
using System.Web;
using IBM.WMQ;
using NUnit.Framework;
using Transports;
using Transports.IBMMQ;

[TestFixture]
class DeadLetterQueueCheckTests
{
    [Test]
    public async Task Should_pass_when_custom_checks_disabled()
    {
        var settings = new TransportSettings
        {
            ConnectionString = ConnectionString,
            RunCustomChecks = false
        };

        var check = new DeadLetterQueueCheck(settings);
        var result = await check.PerformCheck().ConfigureAwait(false);

        Assert.That(result.HasFailed, Is.False);
    }

    [Test]
    public async Task Should_pass_when_dlq_is_empty()
    {
        DrainDeadLetterQueue();

        var settings = new TransportSettings
        {
            ConnectionString = ConnectionString,
            RunCustomChecks = true
        };

        var check = new DeadLetterQueueCheck(settings);
        var result = await check.PerformCheck().ConfigureAwait(false);

        Assert.That(result.HasFailed, Is.False);
    }

    [Test]
    public async Task Should_fail_when_dlq_has_messages()
    {
        DrainDeadLetterQueue();
        PutMessageOnDeadLetterQueue();

        try
        {
            var settings = new TransportSettings
            {
                ConnectionString = ConnectionString,
                RunCustomChecks = true
            };

            var check = new DeadLetterQueueCheck(settings);
            var result = await check.PerformCheck().ConfigureAwait(false);

            Assert.That(result.HasFailed, Is.True);
            Assert.That(result.FailureReason, Does.Contain("messages in the Dead Letter Queue"));
        }
        finally
        {
            DrainDeadLetterQueue();
        }
    }

    [Test]
    public async Task Should_fail_when_connection_is_invalid()
    {
        var settings = new TransportSettings
        {
            ConnectionString = "mq://admin:passw0rd@localhost:19999/BOGUS",
            RunCustomChecks = true
        };

        var check = new DeadLetterQueueCheck(settings);
        var result = await check.PerformCheck().ConfigureAwait(false);

        Assert.That(result.HasFailed, Is.True);
        Assert.That(result.FailureReason, Does.Contain("Unable to check Dead Letter Queue"));
        Assert.That(result.FailureReason, Does.Contain("RC="));
    }

    static void PutMessageOnDeadLetterQueue()
    {
        var (qmName, props) = ParseConnectionString();
        using var qm = new MQQueueManager(qmName, props);
        var dlqName = qm.DeadLetterQueueName.Trim();
        using var dlq = qm.AccessQueue(dlqName, MQC.MQOO_OUTPUT);
        var msg = new MQMessage();
        msg.WriteString("DLQ test message");
        dlq.Put(msg);
    }

    static void DrainDeadLetterQueue()
    {
        var (qmName, props) = ParseConnectionString();
        using var qm = new MQQueueManager(qmName, props);
        var dlqName = qm.DeadLetterQueueName.Trim();
        using var dlq = qm.AccessQueue(dlqName, MQC.MQOO_INPUT_SHARED | MQC.MQOO_FAIL_IF_QUIESCING);
        var gmo = new MQGetMessageOptions { WaitInterval = 0, Options = MQC.MQGMO_NO_WAIT };
        while (true)
        {
            try
            {
                dlq.Get(new MQMessage(), gmo);
            }
            catch (MQException e) when (e.ReasonCode == MQC.MQRC_NO_MSG_AVAILABLE)
            {
                break;
            }
        }
    }

    static (string queueManagerName, Hashtable properties) ParseConnectionString()
    {
        var uri = new Uri(ConnectionString);
        var query = HttpUtility.ParseQueryString(uri.Query);

        var qmName = uri.AbsolutePath.Trim('/') is { Length: > 0 } path
            ? Uri.UnescapeDataString(path)
            : "QM1";

        var props = new Hashtable
        {
            [MQC.TRANSPORT_PROPERTY] = MQC.TRANSPORT_MQSERIES_MANAGED,
            [MQC.HOST_NAME_PROPERTY] = uri.Host,
            [MQC.PORT_PROPERTY] = uri.Port > 0 ? uri.Port : 1414,
            [MQC.CHANNEL_PROPERTY] = query["channel"] ?? "DEV.ADMIN.SVRCONN",
            [MQC.USE_MQCSP_AUTHENTICATION_PROPERTY] = true,
            [MQC.USER_ID_PROPERTY] = Uri.UnescapeDataString(uri.UserInfo.Split(':')[0]),
            [MQC.PASSWORD_PROPERTY] = Uri.UnescapeDataString(uri.UserInfo.Split(':')[1])
        };

        return (qmName, props);
    }

    static readonly string ConnectionString =
        Environment.GetEnvironmentVariable("ServiceControl_TransportTests_IBMMQ_ConnectionString")
        ?? Environment.GetEnvironmentVariable("SERVICECONTROL_TRANSPORTTESTS_IBMMQ_CONNECTIONSTRING")
        ?? "mq://admin:passw0rd@localhost:1414";
}
