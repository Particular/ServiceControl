namespace ServiceControl.MSMQ.DLQMonitor
{
    using System;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using NServiceBus.CustomChecks;
    using NServiceBus.Logging;

    public class CheckDeadLetterQueue : CustomCheck
    {
        public CheckDeadLetterQueue() :
            base("Dead Letter Queue", "Transport", TimeSpan.FromHours(1))
        {
            Logger.Debug("MSMQ Dead Letter Queue custom check starting");

            dlqPerformanceCounter = CreatePerformanceCounter();
        }

        public override Task<CheckResult> PerformCheck()
        {
            Logger.Debug("Checking Dead Letter Queue length");

            if (dlqPerformanceCounter == null)
            {
                var noCounterAvailable = $"Unable to determine the current Dead Letter Queue length on {Environment.MachineName}. This indicates that the performance counter '{EnglishCounterName}' in category '{EnglishCategoryName}' or its localized version was not found.";
                Logger.Warn(noCounterAvailable);
                return CheckResult.Failed(noCounterAvailable);
            }

            var currentValue = dlqPerformanceCounter.NextValue();

            if (currentValue <= 0)
            {
                Logger.Debug("No messages in Dead Letter Queue");
                return CheckResult.Pass;
            }

            var result = $"{currentValue} messages in the Dead Letter Queue on {Environment.MachineName}. This could indicate a problem with ServiceControl's retries. Please submit a support ticket to Particular using support@particular.net if you would like help from our engineers to ensure no message loss while resolving these dead letter messages.";

            Logger.Warn(result);
            return CheckResult.Failed(result);
        }

        static PerformanceCounter CreatePerformanceCounter()
        {
            // English first to stay backward compatible
            return CreatePerformanceCounterIfAvailable(EnglishCategoryName, EnglishCounterName) ??
                   CreateLocalizedPerformanceCounter();
        }

        static PerformanceCounter CreateLocalizedPerformanceCounter()
        {
            var localized = PerformanceCounterHelpers.GetCategoryAndName(EnglishCategoryName, EnglishCounterName);
            // Fully localized first
            return CreatePerformanceCounterIfAvailable(localized.Category, localized.Name) ??
                   CreatePerformanceCounterIfAvailable(EnglishCategoryName, localized.Name) ??
                   CreatePerformanceCounterIfAvailable(localized.Category, EnglishCounterName);
        }

        static PerformanceCounter CreatePerformanceCounterIfAvailable(string categoryName, string counterName)
        {
            try
            {
                return new PerformanceCounter(
                    categoryName: categoryName,
                    counterName: counterName,
                    instanceName: "Computer Queues",
                    readOnly: true);
            }
            catch
            {
                Logger.Debug($"Performance counter '{counterName}' with instance name 'Computer Queues' in category '{categoryName}' was not found.");
                return null;
            }
        }

        PerformanceCounter dlqPerformanceCounter;

        const string EnglishCategoryName = "MSMQ Queue";
        const string EnglishCounterName = "Messages in Queue";

        static readonly ILog Logger = LogManager.GetLogger(typeof(CheckDeadLetterQueue));
    }
}