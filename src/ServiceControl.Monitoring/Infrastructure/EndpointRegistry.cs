namespace ServiceControl.Monitoring.Infrastructure
{
    using System.Collections.Generic;

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
    }
}