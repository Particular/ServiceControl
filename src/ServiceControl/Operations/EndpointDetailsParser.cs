namespace ServiceControl.Contracts.Operations
{
    using System;
    using System.Collections.Generic;
    using Infrastructure;
    using NServiceBus;

    public class EndpointDetailsParser
    {
        public static EndpointDetails SendingEndpoint(IReadOnlyDictionary<string, string> headers)
        {
            var endpointDetails = new EndpointDetails();

            DictionaryExtensions.CheckIfKeyExists(Headers.OriginatingEndpoint, headers, s => endpointDetails.Name = s);
            DictionaryExtensions.CheckIfKeyExists("NServiceBus.OriginatingMachine", headers, s => endpointDetails.Host = s);
            DictionaryExtensions.CheckIfKeyExists(Headers.OriginatingHostId, headers, s => endpointDetails.HostId = Guid.Parse(s));

            if (!string.IsNullOrEmpty(endpointDetails.Name) && !string.IsNullOrEmpty(endpointDetails.Host))
            {
                return endpointDetails;
            }

            string address = null;
            DictionaryExtensions.CheckIfKeyExists(Headers.OriginatingAddress, headers, s => address = s);

            if (address != null)
            {
                var queueAndMachinename = ExtractQueueAndMachineName(address);
                endpointDetails.Name = queueAndMachinename.Queue;
                endpointDetails.Host = queueAndMachinename.Machine;
                return endpointDetails;
            }

            return null;
        }
        
        public static EndpointDetails ReceivingEndpoint(IReadOnlyDictionary<string, string> headers)
        {
            var endpoint = new EndpointDetails();

            if (headers.TryGetValue(Headers.HostId, out var hostIdHeader))
            {
                endpoint.HostId = Guid.Parse(hostIdHeader);
            }

            if (headers.TryGetValue(Headers.HostDisplayName, out var hostDisplayNameHeader))
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

            string address = null;
            //use the failed q to determine the receiving endpoint
            DictionaryExtensions.CheckIfKeyExists("NServiceBus.FailedQ", headers, s => address = s);

            // If we have a failed queue, then construct an endpoint from the failed queue information
            if (address != null)
            {
                var queueAndMachinename = ExtractQueueAndMachineName(address);
                
                if (string.IsNullOrEmpty(endpoint.Name))
                {
                    endpoint.Name = queueAndMachinename.Queue;
                }

                if (string.IsNullOrEmpty(endpoint.Host))
                {
                    endpoint.Host = queueAndMachinename.Machine;
                }

                // If we've been now able to get the endpoint details, return the new info.
                if (!string.IsNullOrEmpty(endpoint.Name) && !string.IsNullOrEmpty(endpoint.Host))
                {
                    return endpoint;
                }
            }

            return null;
        }
        
        static QueueAndMachine ExtractQueueAndMachineName(string address)
        {           
            var atIndex = address?.IndexOf("@", StringComparison.InvariantCulture);

            if (atIndex.HasValue && atIndex.Value > -1)
            {
                var queue = address.Substring(0, atIndex.Value);
                var machine = address.Substring(atIndex.Value + 1);
                return new QueueAndMachine { Queue = queue, Machine = machine };
            }

            return new QueueAndMachine { Queue = address, Machine = null };
        }

        struct QueueAndMachine
        {
            public string Queue;
            public string Machine;
        }
    }
}