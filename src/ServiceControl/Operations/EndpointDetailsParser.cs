namespace ServiceControl.Contracts.Operations
{
    using System;
    using System.Collections.Generic;
    using Infrastructure;
    using NServiceBus;

    public class EndpointDetailsParser
    {
        public static EndpointDetails SendingEndpoint(IDictionary<string, string> headers)
        {
            var endpointDetails = new EndpointDetails();

            DictionaryExtensions.CheckIfKeyExists(Headers.OriginatingEndpoint, headers, s => endpointDetails.Name = s);
            DictionaryExtensions.CheckIfKeyExists("NServiceBus.OriginatingMachine", headers, s => endpointDetails.Host = s);
            DictionaryExtensions.CheckIfKeyExists(Headers.OriginatingHostId, headers, s => endpointDetails.HostId = Guid.Parse(s));

            if (!string.IsNullOrEmpty(endpointDetails.Name) && !string.IsNullOrEmpty(endpointDetails.Host))
            {
                return endpointDetails;
            }

            var address = Address.Undefined;
            DictionaryExtensions.CheckIfKeyExists(Headers.OriginatingAddress, headers, s => address = Address.Parse(s));

            if (address != Address.Undefined)
            {
                endpointDetails.Name = address.Queue;
                endpointDetails.Host = address.Machine;
                return endpointDetails;
            }

            return null;
        }

        public static EndpointDetails ReceivingEndpoint(IDictionary<string, string> headers)
        {
            var endpoint = new EndpointDetails();
            string hostIdHeader;

            if (headers.TryGetValue(Headers.HostId, out hostIdHeader))
            {
                endpoint.HostId = Guid.Parse(hostIdHeader);
            }

            string hostDisplayNameHeader;

            if (headers.TryGetValue(Headers.HostDisplayName, out hostDisplayNameHeader))
            {
                endpoint.Host = hostDisplayNameHeader;
            }
            else
            {
                DictionaryExtensions.CheckIfKeyExists("NServiceBus.ProcessingMachine", headers, s => endpoint.Host = s);
            }

            DictionaryExtensions.CheckIfKeyExists(Headers.ProcessingEndpoint, headers, s => endpoint.Name = s);

            if (!string.IsNullOrEmpty(endpoint.Name) && !string.IsNullOrEmpty(endpoint.Host))
            {
                return endpoint;
            }

            var address = Address.Undefined;
            //use the failed q to determine the receiving endpoint
            DictionaryExtensions.CheckIfKeyExists("NServiceBus.FailedQ", headers, s => address = Address.Parse(s));

            // If we have a failed queue, then construct an endpoint from the failed queue information
            if (address != Address.Undefined)
            {
                if (string.IsNullOrEmpty(endpoint.Name))
                {
                    endpoint.Name = address.Queue;
                }
                if (string.IsNullOrEmpty(endpoint.Host))
                {
                    endpoint.Host = address.Machine;
                }
                return endpoint;
            }
            return null;
        }  
    }
}