namespace ServiceControl.Transports.IBMMQ;

using System;
using System.Threading;
using System.Threading.Tasks;
using IBM.WMQ;
using Microsoft.Extensions.Logging;
using NServiceBus.CustomChecks;
using ServiceControl.Infrastructure;

public class DeadLetterQueueCheck : CustomCheck
{
    public DeadLetterQueueCheck(TransportSettings settings) : base(id: "Dead Letter Queue", category: "Transport", repeatAfter: TimeSpan.FromHours(1))
    {
        Logger.LogDebug("IBM MQ Dead Letter Queue custom check starting");

        (queueManagerName, connectionProperties) = ConnectionProperties.Parse(settings.ConnectionString);
        runCheck = settings.RunCustomChecks;
    }

    public override Task<CheckResult> PerformCheck(CancellationToken cancellationToken = default)
    {
        if (!runCheck)
        {
            return Task.FromResult(CheckResult.Pass);
        }

        Logger.LogDebug("Checking Dead Letter Queue length");

        try
        {
            using var queueManager = new MQQueueManager(queueManagerName, connectionProperties);

            var dlqName = queueManager.DeadLetterQueueName?.Trim();
            if (string.IsNullOrEmpty(dlqName))
            {
                return Task.FromResult(CheckResult.Pass);
            }

            using var dlq = queueManager.AccessQueue(dlqName, MQC.MQOO_INQUIRE | MQC.MQOO_FAIL_IF_QUIESCING);
            var depth = dlq.CurrentDepth;

            if (depth > 0)
            {
                var message = $"{depth} messages in the Dead Letter Queue '{dlqName}' on queue manager '{queueManagerName}'. This could indicate a problem with ServiceControl's retries. Please submit a support ticket to Particular if you would like help from our engineers to ensure no message loss while resolving these dead letter messages.";
                Logger.LogWarning("{DeadLetterMessageCount} messages in the Dead Letter Queue '{DeadLetterQueueName}' on queue manager '{QueueManagerName}'", depth, dlqName, queueManagerName);
                return Task.FromResult(CheckResult.Failed(message));
            }

            Logger.LogDebug("No messages in Dead Letter Queue");
            return Task.FromResult(CheckResult.Pass);
        }
        catch (MQException e)
        {
            var message = $"Unable to check Dead Letter Queue on queue manager '{queueManagerName}'. Reason: {e.Message} (RC={e.ReasonCode})";
            Logger.LogWarning(e, "Unable to check Dead Letter Queue on queue manager '{QueueManagerName}'", queueManagerName);
            return Task.FromResult(CheckResult.Failed(message));
        }
    }

    readonly string queueManagerName;
    readonly System.Collections.Hashtable connectionProperties;
    readonly bool runCheck;

    static readonly ILogger Logger = LoggerUtil.CreateStaticLogger(typeof(DeadLetterQueueCheck));
}