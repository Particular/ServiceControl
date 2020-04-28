namespace ServiceControl.Monitoring.Infrastructure
{
    using ServiceControl.Monitoring.Infrastructure.Extensions;
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public class EndpointRegistry : BreakdownRegistry<EndpointInstanceId>
    {
        public EndpointRegistry() : base(i => i.EndpointName)
        {
        }

        protected override bool AddBreakdown(EndpointInstanceId newEndpointId, Dictionary<EndpointInstanceId, EndpointInstanceId> existingBreakdowns)
        {
            if (existingBreakdowns.TryGetValue(newEndpointId, out var existingInstanceId))
            {
                existingBreakdowns[newEndpointId] = newEndpointId;

                return existingInstanceId.InstanceName != newEndpointId.InstanceName;
            }

            existingBreakdowns.Add(newEndpointId, newEndpointId);

            return true;
        }

        public void RemoveEndpointInstance(string endpointName, string endpointInstance)
        {
            var instances = GetForEndpointName(endpointName).Where(breakdown => string.Equals(breakdown.InstanceId, endpointInstance, StringComparison.OrdinalIgnoreCase));
            instances.ForEach(instance =>
            {
                RemoveBreakdown(instance);
            });
        }
    }
}