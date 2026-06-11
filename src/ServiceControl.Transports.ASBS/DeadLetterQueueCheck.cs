namespace ServiceControl.Transports.ASBS
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Messaging.ServiceBus.Administration;
    using Microsoft.Extensions.Logging;
    using NServiceBus.CustomChecks;

    // This check is intended to detect if messages are accumulating in the staging queue's dead letter queue,
    // which could indicate a problem with ServiceControl's retries. It is not intended to be a general check
    // of the health of the dead letter queue, so it only checks the dead letter queue associated with the staging queue.
    // It is only meant to be running on the primary instance, as the audit and monitoring instances do not have a staging queue.
    public class DeadLetterQueueCheck : CustomCheck
    {
        public DeadLetterQueueCheck(TransportSettings settings, ILogger<DeadLetterQueueCheck> logger) : base(id: "Dead Letter Queue", category: "Transport", repeatAfter: TimeSpan.FromHours(1))
        {
            logger.LogDebug("Azure Service Bus Dead Letter Queue custom check starting");

            this.logger = logger;
            connectionString = settings.ConnectionString;
            stagingQueue = $"{settings.EndpointName}.staging";
            runCheck = settings.RunCustomChecks;
        }

        public override async Task<CheckResult> PerformCheck(CancellationToken cancellationToken = default)
        {
            if (!runCheck)
            {
                return CheckResult.Pass;
            }

            logger.LogDebug("Checking Dead Letter Queue length");
            var managementClient = new ServiceBusAdministrationClient(connectionString);

            var queueRuntimeInfo = await managementClient.GetQueueRuntimePropertiesAsync(stagingQueue, cancellationToken);
            var deadLetterMessageCount = queueRuntimeInfo.Value.DeadLetterMessageCount;

            if (deadLetterMessageCount > 0)
            {
                logger.LogWarning("{DeadLetterMessageCount} messages in the Dead Letter Queue '{StagingQueue}'. This could indicate a problem with ServiceControl's retries. Please submit a support ticket to Particular if you would like help from our engineers to ensure no message loss while resolving these dead letter messages", deadLetterMessageCount, stagingQueue);
                return CheckResult.Failed($"{deadLetterMessageCount} messages in the Dead Letter Queue '{stagingQueue}'. This could indicate a problem with ServiceControl's retries. Please submit a support ticket to Particular if you would like help from our engineers to ensure no message loss while resolving these dead letter messages.");
            }

            logger.LogDebug("No messages in Dead Letter Queue");

            return CheckResult.Pass;
        }

        readonly string connectionString;
        readonly string stagingQueue;
        readonly bool runCheck;
        readonly ILogger<DeadLetterQueueCheck> logger;
    }
}