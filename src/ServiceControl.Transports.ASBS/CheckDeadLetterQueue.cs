namespace ServiceControl.Transports.ASBS
{
    using System;
    using System.Configuration;
    using System.Threading.Tasks;
    using Microsoft.Azure.ServiceBus.Management;
    using NServiceBus.CustomChecks;
    using NServiceBus.Logging;

    public class CheckDeadLetterQueue : CustomCheck
    {
        public CheckDeadLetterQueue(TransportSettings settings) : base(id: "Dead Letter Queue", category: "Transport", repeatAfter: TimeSpan.FromHours(1))
        {
            Logger.Debug("Azure Service Bus Dead Letter Queue custom check starting");

            var connectionStringSettings = ConfigurationManager.ConnectionStrings["NServiceBus/Transport"];
            var transportConnectionString = connectionStringSettings.ConnectionString;
            managementClient = new ManagementClient(transportConnectionString);
            stagingQueue = $"{settings.EndpointName}.staging";
        }

        public async override Task<CheckResult> PerformCheck()
        {
            Logger.Debug("Checking Dead Letter Queue length");

            var queueRuntimeInfo = await managementClient.GetQueueRuntimeInfoAsync(stagingQueue).ConfigureAwait(false);
            var deadLetterMessageCount = queueRuntimeInfo.MessageCountDetails.DeadLetterMessageCount;

            if (deadLetterMessageCount > 0)
            {
                var result = $"{deadLetterMessageCount} messages in the Dead Letter Queue '{stagingQueue}'. This could indicate a problem with ServiceControl's retries. Please submit a support ticket to Particular using support@particular.net if you would like help from our engineers to ensure no message loss while resolving these dead letter messages.";

                Logger.Warn(result);
                return CheckResult.Failed(result);
            }

            Logger.Debug("No messages in Dead Letter Queue");
            return CheckResult.Pass;
        }

        ManagementClient managementClient;
        string stagingQueue;

        static readonly ILog Logger = LogManager.GetLogger(typeof(CheckDeadLetterQueue));
    }
}