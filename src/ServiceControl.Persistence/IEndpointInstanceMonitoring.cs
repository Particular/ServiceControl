namespace ServiceControl.Persistence
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using ServiceControl.Operations;

    public interface IEndpointInstanceMonitoring
    {
        Task CheckEndpoints(DateTime threshold);
        Task DetectEndpointFromHeartbeatStartup(EndpointDetails newEndpointDetails, DateTime startedAt);
        void DetectEndpointFromPersistentStore(EndpointDetails endpointDetails, bool monitored);
        Task DisableMonitoring(Guid id);
        Task EnableMonitoring(Guid id);
        Task EndpointDetected(EndpointDetails newEndpointDetails);
        EndpointsView[] GetEndpoints();
        List<KnownEndpointsView> GetKnownEndpoints();
        EndpointMonitoringStats GetStats();
        bool HasEndpoint(Guid endpointId);
        bool IsMonitored(Guid id);
        bool IsNewInstance(EndpointDetails newEndpointDetails);
        void RecordHeartbeat(EndpointInstanceId endpointInstanceId, DateTime timestamp);
        void RemoveEndpoint(Guid endpointId);
    }
}