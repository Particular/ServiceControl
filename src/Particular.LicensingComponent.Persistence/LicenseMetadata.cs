namespace Particular.LicensingComponent.Persistence
{
    using System;
    using Particular.LicensingComponent.Contracts;

    public static class LicenseMetadata
    {
        extension(LicensedEndpointDetails baseDetails)
        {
            public LicensedEndpointDetails Amend(LicensedEndpointDetailsAmendment? amendments)
            {
                if (amendments is null)
                {
                    return baseDetails;
                }

                //Note: baseDetails.UnassignedQueues will always be empty before amendments
                baseDetails.UnassignedQueues = amendments.UnassignedQueues;
                baseDetails.ExcludedQueues = [.. baseDetails.ExcludedQueues.Except([.. amendments.UnassignedQueues, .. amendments.UnmappedQueues, .. amendments.InfrastructureQueues, .. amendments.Endpoints.SelectMany(queue => queue.AmendedQueues)]), .. amendments.ExcludedQueues];
                baseDetails.InfrastructureQueues = [.. baseDetails.InfrastructureQueues.Except([.. amendments.UnassignedQueues, .. amendments.UnmappedQueues, .. amendments.ExcludedQueues, .. amendments.Endpoints.SelectMany(queue => queue.AmendedQueues)]), .. amendments.InfrastructureQueues];
                baseDetails.Endpoints = [
                    .. baseDetails.Endpoints.Where(endpoint => !amendments.RemovedEndpointIds.Contains(endpoint.ClientId))
                        .Select(endpoint =>
                        {
                            var amendment = amendments.Endpoints.FirstOrDefault(amendment => amendment.ClientId == endpoint.ClientId);
                            return endpoint with
                            {
                                Name = amendment?.Name ?? endpoint.Name,
                                Classification = amendment?.Classification ?? endpoint.Classification,
                                Queues = [.. endpoint.Queues.Except([.. amendments.UnassignedQueues, .. amendments.UnmappedQueues, .. amendments.ExcludedQueues, .. amendments.InfrastructureQueues, .. amendments.Endpoints.SelectMany(queue => queue.AmendedQueues)]), .. amendment?.AmendedQueues ?? []]
                            };
                        }),
                    .. amendments.Endpoints.Where(endpoint => !baseDetails.Endpoints.Any(baseEndpoint => baseEndpoint.ClientId == endpoint.ClientId))
                        .Select(endpoint => new LicensedEndpoint
                        {
                            ClientId = endpoint.ClientId,
                            Name = endpoint.Name,
                            Classification = endpoint.Classification,
                            Queues = endpoint.AmendedQueues
                        })];

                return baseDetails;
            }
        }
    }
}
