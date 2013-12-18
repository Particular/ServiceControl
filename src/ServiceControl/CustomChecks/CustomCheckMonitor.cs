namespace ServiceControl.CustomChecks
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using Contracts.CustomChecks;
    using Contracts.Operations;
    using Infrastructure;
    using NServiceBus;
    using Plugin.CustomChecks.Messages;

    public class CustomCheckMonitor
    {
        readonly IBus bus;

        readonly ConcurrentDictionary<Guid, ReportCustomCheckResult> registeredCustomChecks =
            new ConcurrentDictionary<Guid, ReportCustomCheckResult>();

        public CustomCheckMonitor(IBus bus)
        {
            this.bus = bus;
        }

        public void RegisterResult(ReportCustomCheckResult result, IDictionary<string, string> headers)
        {
            var originatingEndpoint = EndpointDetails.SendingEndpoint(headers);

            var key = DeterministicGuid.MakeId(result.CustomCheckId, originatingEndpoint.Name, originatingEndpoint.Machine);

            var isRegistered = registeredCustomChecks.ContainsKey(key);
            if (isRegistered)
            {
                // Raise an alert if there has been a change in status
                if (registeredCustomChecks[key].Result.HasFailed == result.Result.HasFailed)
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
                    m.OriginatingEndpoint = originatingEndpoint;
                });
            }
            else if (isRegistered) // We only publish successes if status has changed. 
            {
                bus.Publish<CustomCheckSucceeded>(m =>
                {
                    m.CustomCheckId = result.CustomCheckId;
                    m.Category = result.Category;
                    m.SucceededAt = result.ReportedAt;
                    m.OriginatingEndpoint = originatingEndpoint;
                });
            }

            registeredCustomChecks[key] = result;
        }
    }
}
