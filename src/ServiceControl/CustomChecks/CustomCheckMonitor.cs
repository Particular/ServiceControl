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

    class CustomCheckMonitor : INeedInitialization
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
            var originatingEndpoint = EndpointDetailsParser.SendingEndpoint(headers);
            originatingEndpoint.HostId = message.HostId;

            var key = DeterministicGuid.MakeId(message.CustomCheckId, originatingEndpoint.Name, originatingEndpoint.Host);
            var publish = true;

            registeredCustomChecks.AddOrUpdate(key,
                k => message,
                (k, existingValue) =>
                {
                    if (existingValue.HasFailed == message.HasFailed)
                    {
                        publish = false;
                        return existingValue;
                    }

                    return message;
                });

            if (!publish)
            {
                return;
            }

            if (message.HasFailed)
            {
                bus.Publish<CustomCheckFailed>(m =>
                {
                    m.CustomCheckId = message.CustomCheckId;
                    m.Category = message.Category;
                    m.FailedAt = message.ReportedAt;
                    m.FailureReason = message.FailureReason;
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