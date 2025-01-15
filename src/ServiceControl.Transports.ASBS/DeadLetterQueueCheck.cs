﻿namespace ServiceControl.Transports.ASBS
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Messaging.ServiceBus.Administration;
    using NServiceBus.CustomChecks;
    using NServiceBus.Logging;

    public class DeadLetterQueueCheck : CustomCheck
    {
        public DeadLetterQueueCheck(TransportSettings settings) : base(id: "Dead Letter Queue", category: "Transport", repeatAfter: TimeSpan.FromHours(1))
        {
            Logger.Debug("Azure Service Bus Dead Letter Queue custom check starting");

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

            Logger.Debug("Checking Dead Letter Queue length");
            var managementClient = new ServiceBusAdministrationClient(connectionString);

            var queueRuntimeInfo = await managementClient.GetQueueRuntimePropertiesAsync(stagingQueue, cancellationToken);
            var deadLetterMessageCount = queueRuntimeInfo.Value.DeadLetterMessageCount;

            if (deadLetterMessageCount > 0)
            {
                var result = $"{deadLetterMessageCount} messages in the Dead Letter Queue '{stagingQueue}'. This could indicate a problem with ServiceControl's retries. Please submit a support ticket to Particular if you would like help from our engineers to ensure no message loss while resolving these dead letter messages.";

                Logger.Warn(result);
                return CheckResult.Failed(result);
            }

            Logger.Debug("No messages in Dead Letter Queue");

            return CheckResult.Pass;
        }

        string connectionString;
        string stagingQueue;
        bool runCheck;


        static readonly ILog Logger = LogManager.GetLogger(typeof(DeadLetterQueueCheck));
    }
}