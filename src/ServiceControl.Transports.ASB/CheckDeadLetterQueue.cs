namespace ServiceControl.Transports.ASB
{
    using System;
    using System.Configuration;
    using System.Threading.Tasks;
    using NServiceBus.CustomChecks;
    using Microsoft.ServiceBus;
    using NServiceBus.Logging;

    public class CheckDeadLetterQueue : CustomCheck
    {
        NamespaceManager namespaceManager;
        string stagingQueue;

        public CheckDeadLetterQueue(TransportSettings settings) : base(id: "Dead Letter Queue", category: "Transport", repeatAfter: TimeSpan.FromHours(1))
        {
            Logger.Debug("Azure Service Bus Dead Letter Queue custom check starting");

            var connectionStringSettings = ConfigurationManager.ConnectionStrings["NServiceBus/Transport"];
            var transportConnectionString = connectionStringSettings.ConnectionString;
            namespaceManager = NamespaceManager.CreateFromConnectionString(transportConnectionString);
            stagingQueue = $"{settings.EndpointName}.staging";
        }

        public override Task<CheckResult> PerformCheck()
        {
            Logger.Debug("Checking Dead Letter Queue length");

            var queueDescription = namespaceManager.GetQueue(stagingQueue);
            var messageCountDetails = queueDescription.MessageCountDetails;

            if (messageCountDetails.DeadLetterMessageCount > 0)
            {
                var result = $"{messageCountDetails.DeadLetterMessageCount} messages in the Dead Letter Queue '{stagingQueue}'. This could indicate a problem with ServiceControl's retries. Please submit a support ticket to Particular using support@particular.net if you would like help from our engineers to ensure no message loss while resolving these dead letter messages.";

                Logger.Warn(result);
                return CheckResult.Failed(result);
            }

            Logger.Debug("No messages in Dead Letter Queue");
            return CheckResult.Pass;
        }

        static readonly ILog Logger = LogManager.GetLogger(typeof(CheckDeadLetterQueue));
    }
}