namespace ServiceControl.MSMQ.DLQMonitor
{
    using System;
    using System.Configuration;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using NServiceBus.CustomChecks;
    using Transports;

    public class DeadLetterQueueCheck : CustomCheck
    {
        public DeadLetterQueueCheck(TransportSettings settings, ILogger<DeadLetterQueueCheck> logger) :
            base("Dead Letter Queue", "Transport", TimeSpan.FromHours(1))
        {
            runCheck = settings.RunCustomChecks;
            if (!runCheck)
            {
                return;
            }

            logger.LogDebug("MSMQ Dead Letter Queue custom check starting");

            categoryName = Read("Msmq/PerformanceCounterCategoryName", "MSMQ Queue");
            counterName = Read("Msmq/PerformanceCounterName", "Messages in Queue");
            counterInstanceName = Read("Msmq/PerformanceCounterInstanceName", "Computer Queues");

            try
            {
                dlqPerformanceCounter = new PerformanceCounter(categoryName, counterName, counterInstanceName, readOnly: true);
            }
            catch (InvalidOperationException ex)
            {
                logger.LogError(ex, CounterMightBeLocalized("CategoryName", "CounterName", "CounterInstanceName"), categoryName, counterName, counterInstanceName);
            }

            this.logger = logger;
        }

        public override Task<CheckResult> PerformCheck(CancellationToken cancellationToken = default)
        {
            if (!runCheck)
            {
                return CheckResult.Pass;
            }

            logger.LogDebug("Checking Dead Letter Queue length");
            float currentValue;
            try
            {
                if (dlqPerformanceCounter == null)
                {
                    throw new InvalidOperationException("Unable to create performance counter instance.");
                }

                currentValue = dlqPerformanceCounter.NextValue();
            }
            catch (InvalidOperationException ex)
            {
                logger.LogWarning(ex, CounterMightBeLocalized("CategoryName", "CounterName", "CounterInstanceName"), categoryName, counterName, counterInstanceName);
                return CheckResult.Failed(CounterMightBeLocalized(categoryName, counterName, counterInstanceName));
            }

            if (currentValue <= 0)
            {
                logger.LogDebug("No messages in Dead Letter Queue");
                return CheckResult.Pass;
            }

            logger.LogWarning("{DeadLetterMessageCount} messages in the Dead Letter Queue on {MachineName}. This could indicate a problem with ServiceControl's retries. Please submit a support ticket to Particular if you would like help from our engineers to ensure no message loss while resolving these dead letter messages", currentValue, Environment.MachineName);
            return CheckResult.Failed($"{currentValue} messages in the Dead Letter Queue on {Environment.MachineName}. This could indicate a problem with ServiceControl's retries. Please submit a support ticket to Particular if you would like help from our engineers to ensure no message loss while resolving these dead letter messages.");
        }

        static string CounterMightBeLocalized(string categoryName, string counterName, string counterInstanceName)
        {
            return
                $"Unable to read the Dead Letter Queue length. The performance counter with category '{categoryName}' and name '{counterName}' and instance name '{counterInstanceName}' is not available. "
                + "It is possible that the counter category, name and instance name have been localized into different languages. "
                + @"Consider overriding the counter category, name and instance name in the application configuration file by adding:
   <appSettings>
     <add key=""ServiceControl/Msmq/PerformanceCounterCategoryName"" value=""LocalizedCategoryName"" />
     <add key=""ServiceControl/Msmq/PerformanceCounterName"" value=""LocalizedCounterName"" />
     <add key=""ServiceControl/Msmq/PerformanceCounterInstanceName"" value=""LocalizedCounterInstanceName"" />
   </appSettings>
";
        }

        // from ConfigFileSettingsReader since we cannot reference ServiceControl
        static string Read(string name, string defaultValue = default)
        {
            return Read("ServiceControl", name, defaultValue);
        }

        static string Read(string root, string name, string defaultValue = default)
        {
            return TryRead(root, name, out var value) ? value : defaultValue;
        }

        static bool TryRead(string root, string name, out string value)
        {
            var fullKey = $"{root}/{name}";

            if (ConfigurationManager.AppSettings[fullKey] != null)
            {
                value = ConfigurationManager.AppSettings[fullKey];
                return true;
            }

            value = default;
            return false;
        }

        PerformanceCounter dlqPerformanceCounter;
        string categoryName;
        string counterName;
        string counterInstanceName;
        bool runCheck;

        readonly ILogger logger;
    }
}