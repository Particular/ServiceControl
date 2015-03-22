namespace ServiceControl.Contracts.Operations
{
    using System;
    using Particular.Operations.Ingestion.Api;

    public class EndpointInstanceParser
    {
        public EndpointInstance ParseSendingEndpoint(HeaderCollection headers)
        {
            string endpointName;
            string hostId;
            if (headers.TryGet("NServiceBus.OriginatingEndpoint", out endpointName) 
                && ParseOriginatingHostId(headers, out hostId))
            {
                return new EndpointInstance(endpointName, hostId);
            }

            string originatingAddress;
            if (headers.TryGet("NServiceBus.OriginatingAddress", out originatingAddress))
            {
                return GuessInstanceIdFromAddress(originatingAddress);
            }
            return EndpointInstance.Unknown;
        }

        public EndpointInstance ParseProcessingEndpoint(HeaderCollection headers)
        {
            string endpointName;
            string hostId;
            
            if (headers.TryGet("NServiceBus.ProcessingEndpoint", out endpointName)
                && ParseProcessingHostId(headers, out hostId))
            {
                return new EndpointInstance(endpointName, hostId);
            }

            string failedQueueAddress;
            if (headers.TryGet("NServiceBus.FailedQ", out failedQueueAddress))
            { 
                return GuessInstanceIdFromAddress(failedQueueAddress);
            }
            return EndpointInstance.Unknown;
        }

        static bool ParseProcessingHostId(HeaderCollection headers, out string hostId)
        {
            return headers.TryGet("$.diagnostics.hostid", out hostId)
                || headers.TryGet("$.diagnostics.hostdisplayname", out hostId)
                || headers.TryGet("NServiceBus.ProcessingMachine", out hostId);
        }

        static EndpointInstance GuessInstanceIdFromAddress(string originatingAddress)
        {
            var queueAndMachine = originatingAddress.Split('@');
            var machine = "Unknown";
            if (queueAndMachine.Length > 1
                && !queueAndMachine[1].Equals("localhost", StringComparison.InvariantCultureIgnoreCase)
                && !queueAndMachine[1].Equals("127.0.0.1", StringComparison.InvariantCultureIgnoreCase))
            {
                machine = queueAndMachine[1];
            }
            return new EndpointInstance(queueAndMachine[0], machine);
        }

        static bool ParseOriginatingHostId(HeaderCollection headers, out string hostId)
        {
            return headers.TryGet("$.diagnostics.originating.hostid", out hostId)
                   || headers.TryGet("NServiceBus.OriginatingMachine", out hostId);
        }

    }
}