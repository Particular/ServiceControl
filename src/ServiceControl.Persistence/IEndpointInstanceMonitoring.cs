namespace ServiceControl.Persistence
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using ServiceControl.Operations;

    public interface IEndpointInstanceMonitoring
    {
        Task CheckEndpoints(DateTime threshold, CancellationToken cancellationToken = default);
        Task DetectEndpointFromHeartbeatStartup(EndpointDetails newEndpointDetails, DateTime startedAt, CancellationToken cancellationToken = default);
        void DetectEndpointFromPersistentStore(EndpointDetails endpointDetails, bool monitored);
        Task DisableMonitoring(Guid id, CancellationToken cancellationToken = default);
        Task EnableMonitoring(Guid id, CancellationToken cancellationToken = default);
        Task EndpointDetected(EndpointDetails newEndpointDetails, CancellationToken cancellationToken = default);
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