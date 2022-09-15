namespace ServiceControl.Audit.Persistence.Monitoring
{
    using System;
    using Infrastructure;
    using ServiceControl.Audit.Persistence.Infrastructure;

    public class KnownEndpoint
    {
        public string Id { get; set; }

        public string Name { get; set; }

        public Guid HostId { get; set; }

        public string Host { get; set; }

        public DateTime LastSeen { get; set; }

        internal static string MakeDocumentId(string endpointName, Guid endpointHostId)
        {
            return $"{CollectionName}/{DeterministicGuid.MakeId(endpointName, endpointHostId.ToString())}";
        }

        public const string CollectionName = "KnownEndpoints";
    }
}