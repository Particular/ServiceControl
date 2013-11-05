namespace ServiceControl.CustomChecks
{
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using Contracts.CustomChecks;
    using EndpointPlugin.Messages.CustomChecks;
    using NServiceBus;
    using ServiceBus.Management.MessageAuditing;

    public class CustomCheckMonitor
    {
        readonly IBus bus;

        readonly ConcurrentDictionary<string, ReportCustomCheckResult> registeredCustomChecks =
            new ConcurrentDictionary<string, ReportCustomCheckResult>();

        public CustomCheckMonitor(IBus bus)
        {
            this.bus = bus;
        }

        public void RegisterResult(ReportCustomCheckResult result, IDictionary<string, string> headers)
        {
            var isRegistered = registeredCustomChecks.ContainsKey(result.CustomCheckId);
            if (isRegistered)
            {
                // Raise an alert if there has been a change in status
                if (registeredCustomChecks[result.CustomCheckId].Result.HasFailed == result.Result.HasFailed)
                {
                    return;
                }
            }

            if (result.Result.HasFailed)
            {
                bus.Publish<CustomCheckFailed>(m =>
                {
                    m.CustomCheckId = result.CustomCheckId;
                    m.Category = result.Category;
                    m.FailedAt = result.ReportedAt;
                    m.FailureReason = result.Result.FailureReason;
                    m.OriginatingEndpoint = EndpointDetails.OriginatingEndpoint(headers);
                });
            }
            else if (isRegistered) // We only publish successes if status has changed. 
            {
                bus.Publish<CustomCheckSucceeded>(m =>
                {
                    m.CustomCheckId = result.CustomCheckId;
                    m.Category = result.Category;
                    m.SucceededAt = result.ReportedAt;
                    m.OriginatingEndpoint = EndpointDetails.OriginatingEndpoint(headers);
                });
            }

            registeredCustomChecks[result.CustomCheckId] = result;
        }
    }
}
