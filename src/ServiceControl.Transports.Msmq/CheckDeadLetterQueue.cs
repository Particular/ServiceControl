namespace ServiceControl.MSMQ.DLQMonitor
{
    using System;
    using System.Configuration;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using NServiceBus.CustomChecks;
    using NServiceBus.Logging;
    using Transports;

    public class CheckDeadLetterQueue : CustomCheck
    {
        public CheckDeadLetterQueue(TransportSettings settings) :
            base("Dead Letter Queue", CustomChecksCategories.ServiceControlTransportHealth, TimeSpan.FromHours(1))
        {
            runCheck = settings.RunCustomChecks;
            if (!runCheck)
            {
                return;
            }

            Logger.Debug("MSMQ Dead Letter Queue custom check starting");

            categoryName = Read("Msmq/PerformanceCounterCategoryName", "MSMQ Queue");
            counterName = Read("Msmq/PerformanceCounterName", "Messages in Queue");
            counterInstanceName = Read("Msmq/PerformanceCounterInstanceName", "Computer Queues");

            try
            {
                dlqPerformanceCounter = new PerformanceCounter(categoryName, counterName, counterInstanceName, readOnly: true);
            }
            catch (InvalidOperationException ex)
            {
                Logger.Error(CounterMightBeLocalized(categoryName, counterName, counterInstanceName), ex);
            }
        }

        public override Task<CheckResult> PerformCheck()
        {
            if (!runCheck)
            {
                return CheckResult.Pass;
            }

            Logger.Debug("Checking Dead Letter Queue length");
            float currentValue;
            string result;
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
                result = CounterMightBeLocalized(categoryName, counterName, counterInstanceName);
                Logger.Warn(result, ex);
                return CheckResult.Failed(result);
            }

            if (currentValue <= 0)
            {
                Logger.Debug("No messages in Dead Letter Queue");
                return CheckResult.Pass;
            }

            result = MessagesInDeadLetterQueue(currentValue);
            Logger.Warn(result);
            return CheckResult.Failed(result);
        }

        static string MessagesInDeadLetterQueue(float currentValue)
        {
            return $"{currentValue} messages in the Dead Letter Queue on {Environment.MachineName}. This could indicate a problem with ServiceControl's retries. Please submit a support ticket to Particular using support@particular.net if you would like help from our engineers to ensure no message loss while resolving these dead letter messages.";
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

        static readonly ILog Logger = LogManager.GetLogger(typeof(CheckDeadLetterQueue));
    }
}