namespace ServiceControl.Contracts.Operations
{
    using System.Collections.Generic;
    using Infrastructure;
    using NServiceBus;

    public class EndpointDetails
    {
        public string Name { get; set; }

        public string Machine { get; set; }

        public static EndpointDetails SendingEndpoint(IDictionary<string,string> headers )
        {
            var endpointDetails = new EndpointDetails();
            DictionaryExtensions.CheckIfKeyExists(Headers.OriginatingEndpoint, headers, s => endpointDetails.Name = s );
            DictionaryExtensions.CheckIfKeyExists(Headers.OriginatingMachine, headers, s => endpointDetails.Machine = s);

            if (!string.IsNullOrEmpty(endpointDetails.Name) && !string.IsNullOrEmpty(endpointDetails.Machine))
            {
                return endpointDetails;
            }

            var address = Address.Undefined; 
            DictionaryExtensions.CheckIfKeyExists(Headers.OriginatingAddress, headers, s => address = Address.Parse(s));

            if (address != Address.Undefined)
            {
                endpointDetails.Name = address.Queue;
                endpointDetails.Machine = address.Machine;
                return endpointDetails;
            }

            return null;
        }

        public static EndpointDetails ReceivingEndpoint(IDictionary<string,string> headers)
        {
            var endpoint = new EndpointDetails();
            DictionaryExtensions.CheckIfKeyExists(Headers.ProcessingEndpoint, headers, s => endpoint.Name = s);
            DictionaryExtensions.CheckIfKeyExists(Headers.ProcessingMachine, headers, s => endpoint.Machine = s);

            if (!string.IsNullOrEmpty(endpoint.Name) && !string.IsNullOrEmpty(endpoint.Machine))
            {
                return endpoint;
            }

            var address = Address.Undefined;
            // TODO: do we need the below for the originating address!?
            DictionaryExtensions.CheckIfKeyExists(Headers.OriginatingAddress, headers, s => address = Address.Parse(s));
            //use the failed q to determine the receiving endpoint
            DictionaryExtensions.CheckIfKeyExists("NServiceBus.FailedQ", headers, s => address = Address.Parse(s));
            
            if (string.IsNullOrEmpty(endpoint.Name))
            {
                endpoint.Name = address.Queue;
            }

            if (string.IsNullOrEmpty(endpoint.Machine))
            {
                endpoint.Machine = address.Machine;
            }

            return endpoint;
        }
    }
}