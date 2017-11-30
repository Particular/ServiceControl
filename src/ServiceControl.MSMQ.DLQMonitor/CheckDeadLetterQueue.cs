namespace ServiceControl.MSMQ.DLQMonitor
{
    using System;
    using NServiceBus.CustomChecks;
    using System.Diagnostics;

    public class CheckDeadLetterQueue : CustomCheck
    {
        PerformanceCounter dlqPerformanceCounter;

        public CheckDeadLetterQueue() :
            base("Dead Letter Queue", "Transport", TimeSpan.FromHours(1))
        {
            dlqPerformanceCounter = new PerformanceCounter(
                categoryName: "MSMQ Queue",
                counterName: "Messages in Queue",
                instanceName: "Computer Queues",
                readOnly: true);
        }

        public override CheckResult PerformCheck()
        {
            var currentValue = dlqPerformanceCounter.NextValue();

            if (currentValue <= 0)
            {
                return CheckResult.Pass;
            }

            return CheckResult.Failed($"{currentValue} messages in the Dead Letter Queue on {Environment.MachineName}. This could indicate a problem with ServiceControl's retries. Please submit a support ticket to Particular using support@particular.net if you would like help from our engineers to ensure no message loss while resolving these dead letter messages.");
        }

    }
}