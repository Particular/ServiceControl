namespace Particular.Operations.Errors.Api
{
    using System.Collections.Generic;
    using ServiceControl.Contracts.Operations;

    public class ErrorMessage
    {
        public ErrorMessage(IReadOnlyDictionary<string, string> headers, EndpointDetails sendingEndpoint, EndpointDetails processingEndpoint)
        {
            Headers = headers;
            SendingEndpoint = sendingEndpoint;
            ProcessingEndpoint = processingEndpoint;
        }

        public IReadOnlyDictionary<string, string> Headers { get; }

        /// <summary>
        /// SendingEndpoint will be null for messages that are from v3.3.x endpoints because we don't
        /// have the relevant information via the headers, which were added in v4.
        /// </summary>
        public EndpointDetails SendingEndpoint { get; }

        /// <summary>
        /// The ProcessingEndpoint will be null for messages from v3.3.x endpoints that were successfully
        /// processed because we don't have the information from the relevant headers.
        /// </summary>
        public EndpointDetails ProcessingEndpoint { get; }
    }
}