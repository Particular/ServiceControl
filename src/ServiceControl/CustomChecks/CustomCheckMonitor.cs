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

    public class CustomCheckMonitor : INeedInitialization
    {
        public CustomCheckMonitor()
        {
            // Need this because INeedInitialization does not use DI instead use Activator.CreateInstance
        }

        public CustomCheckMonitor(IBus bus)
        {
            this.bus = bus;
        }

        public void Init()
        {
            Configure.Component<CustomCheckMonitor>(DependencyLifecycle.SingleInstance);
        }

        public void RegisterResult(ReportCustomCheckResult message, IDictionary<string, string> headers)
        {
            var originatingEndpoint = EndpointDetails.SendingEndpoint(headers);
            var key = DeterministicGuid.MakeId(message.CustomCheckId, originatingEndpoint.Name, originatingEndpoint.Host);
            var publish = false;

            registeredCustomChecks.AddOrUpdate(key,
                k => message,
                (k, existingValue) =>
                {
                    if (existingValue.Result.HasFailed == message.Result.HasFailed)
                    {
                        return existingValue;
                    }

                    publish = true;
                    
                    return message;
                });

            if (!publish)
            {
                return;
            }

            if (message.Result.HasFailed)
            {
                bus.Publish<CustomCheckFailed>(m =>
                {
                    m.CustomCheckId = message.CustomCheckId;
                    m.Category = message.Category;
                    m.FailedAt = message.ReportedAt;
                    m.FailureReason = message.Result.FailureReason;
                    m.OriginatingEndpoint = originatingEndpoint;
                });
            }
            else
            {
                bus.Publish<CustomCheckSucceeded>(m =>
                {
                    m.CustomCheckId = message.CustomCheckId;
                    m.Category = message.Category;
                    m.SucceededAt = message.ReportedAt;
                    m.OriginatingEndpoint = originatingEndpoint;
                });
            }
        }

        readonly IBus bus;

        readonly ConcurrentDictionary<Guid, ReportCustomCheckResult> registeredCustomChecks =
            new ConcurrentDictionary<Guid, ReportCustomCheckResult>();
    }
}