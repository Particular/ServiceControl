namespace ServiceControl.CustomChecks
{
    using System;
    using System.Threading.Tasks;
    using Contracts.CustomChecks;

    interface ICustomChecksBackend
    {
        Task UpdateCustomCheckStatus(EndpointDetails originatingEndpoint, DateTime reportedAt, string customCheckId, string category, bool hasFailed, string failureReason);
    }
}