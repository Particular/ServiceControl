namespace ServiceControl.Contracts.Operations
{
    using System.Linq;
    using Particular.Backend.AuditIngestion.Api;
    using ServiceControl.Shell.Api.Ingestion;

    public class EndpointDetailsParser
    {
        public EndpointInstanceId ParseSendingEndpoint(HeaderCollection headers)
        {
            string endpointName;
            string hostId;
            if (headers.TryGet("NServiceBus.OriginatingEndpoint", out endpointName) &&
                headers.TryGet("$.diagnostics.originating.hostid", out hostId))
            {
                return new EndpointInstanceId(endpointName, hostId);
            }

            string originatingAddress;
            if (headers.TryGet("NServiceBus.OriginatingAddress", out originatingAddress))
            {
                var queue = originatingAddress.Split('@').First();
                return new EndpointInstanceId(queue, null);
            }
            return null;
        }

        public  EndpointInstanceId ParseProcessingEndpoint(HeaderCollection headers)
        {
            string endpointName;
            string hostId;
            
            if (headers.TryGet("$.diagnostics.hostid", out hostId)
                && headers.TryGet("NServiceBus.ProcessingEndpoint", out endpointName))
            {
                return new EndpointInstanceId(endpointName, hostId);
            }
            return null;
        }  
    }
}