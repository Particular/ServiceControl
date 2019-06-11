namespace ServiceControl.Audit.Monitoring
{
    using System;
    using System.Collections.Concurrent;
    using Infrastructure;

    class EndpointInstanceMonitoring
    {
        public bool IsNewInstance(EndpointDetails newEndpointDetails)
        {
            var endpointInstanceId = DeterministicGuid.MakeId(newEndpointDetails.Name, newEndpointDetails.HostId.ToString());

            return endpoints.TryAdd(endpointInstanceId, null);
        }

        ConcurrentDictionary<Guid, object> endpoints = new ConcurrentDictionary<Guid, object>();
    }
}