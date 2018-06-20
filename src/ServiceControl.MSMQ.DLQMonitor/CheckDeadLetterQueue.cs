namespace ServiceControl.MSMQ.DLQMonitor
{
    using System;
    using NServiceBus.CustomChecks;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using NServiceBus.Logging;

    public class CheckDeadLetterQueue : CustomCheck
    {
        PerformanceCounter dlqPerformanceCounter;

        public CheckDeadLetterQueue() :
            base("Dead Letter Queue", "Transport", TimeSpan.FromHours(1))
        {
            Logger.Debug("MSMQ Dead Letter Queue custom check starting");

            dlqPerformanceCounter = new PerformanceCounter(
                categoryName: "MSMQ Queue",
                counterName: "Messages in Queue",
                instanceName: "Computer Queues",
                readOnly: true);
        }

        public override Task<CheckResult> PerformCheck()
        {
            Logger.Debug("Checking Dead Letter Queue length");
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

        static readonly ILog Logger = LogManager.GetLogger(typeof(CheckDeadLetterQueue));
    }
}