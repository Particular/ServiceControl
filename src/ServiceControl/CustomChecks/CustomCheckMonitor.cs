namespace ServiceControl.CustomChecks
{
    using System.Collections.Concurrent;
    using Contracts.CustomChecks;
    using EndpointPlugin.Messages.CustomChecks;
    using NServiceBus;

    public class CustomCheckMonitor
    {
        readonly IBus bus;

        readonly ConcurrentDictionary<string, ReportCustomCheckResult> registeredCustomChecks =
            new ConcurrentDictionary<string, ReportCustomCheckResult>();

        public CustomCheckMonitor(IBus bus)
        {
            this.bus = bus;
        }

        public void RegisterResult(ReportCustomCheckResult result)
        {
            if (registeredCustomChecks.ContainsKey(result.CustomCheckId))
            {
                // Raise an alert if there has been a change in status
                if (registeredCustomChecks[result.CustomCheckId].Result.HasFailed == result.Result.HasFailed) return;
                if (result.Result.HasFailed)
                {
                    bus.Publish<CustomCheckFailed>(m =>
                    {
                        m.CustomCheckId = result.CustomCheckId;
                        m.Category = result.Category;
                        m.FailedAt = result.ReportedAt;
                        m.FailureReason = result.Result.FailureReason;
                    });
                }
                else
                {
                    bus.Publish<CustomCheckSucceeded>(m =>
                    {
                        m.CustomCheckId = result.CustomCheckId;
                        m.Category = result.Category;
                        m.SucceededAt = result.ReportedAt;
                    });
                }
            }
            else
            {
                if (result.Result.HasFailed)
                {
                    bus.Publish<CustomCheckFailed>(m =>
                    {
                        m.CustomCheckId = result.CustomCheckId;
                        m.Category = result.Category;
                        m.FailedAt = result.ReportedAt;
                        m.FailureReason = result.Result.FailureReason;
                    });
                }
            }

            // either add a new item or Update the dictionary
            registeredCustomChecks.AddOrUpdate(result.CustomCheckId, result, (key, oldValue) => result);
        }
    }
}
