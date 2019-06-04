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
            connectionString = connectionStringSettings.ConnectionString;
            stagingQueue = $"{settings.EndpointName}.staging";
            runCheck = settings.RunCustomChecks;
        }

        public override async Task<CheckResult> PerformCheck()
        {
            if (!runCheck)
            {
                return CheckResult.Pass;
            }

            Logger.Debug("Checking Dead Letter Queue length");
            var managementClient = new ManagementClient(connectionString);

            try
            {
                var queueRuntimeInfo = await managementClient.GetQueueRuntimeInfoAsync(stagingQueue).ConfigureAwait(false);
                var deadLetterMessageCount = queueRuntimeInfo.MessageCountDetails.DeadLetterMessageCount;

                if (deadLetterMessageCount > 0)
                {
                    var result = $"{deadLetterMessageCount} messages in the Dead Letter Queue '{stagingQueue}'. This could indicate a problem with ServiceControl's retries. Please submit a support ticket to Particular using support@particular.net if you would like help from our engineers to ensure no message loss while resolving these dead letter messages.";

                    Logger.Warn(result);
                    return CheckResult.Failed(result);
                }

                Logger.Debug("No messages in Dead Letter Queue");
            }
            finally
            {
                await managementClient.CloseAsync().ConfigureAwait(false);
            }

            return CheckResult.Pass;
        }

        string connectionString;
        string stagingQueue;
        bool runCheck;


        static readonly ILog Logger = LogManager.GetLogger(typeof(CheckDeadLetterQueue));
    }
}