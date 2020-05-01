namespace ServiceControl.Recoverability
{
    using System.Collections.Generic;
    using Contracts.Operations;

    public class EndpointNameClassifier : IFailureClassifier
    {
        public string Name => Id;

        public string ClassifyFailure(ClassifiableMessageDetails failureDetails)
        {
            return failureDetails.ProcessingAttempt == null ? null : ExtractEndpointName(failureDetails.ProcessingAttempt.Headers);
        }

        static string ExtractEndpointName(IReadOnlyDictionary<string, string> headers)
        {
            var details = EndpointDetailsParser.ReceivingEndpoint(headers);
            return details?.Name;
        }

        const string Id = "Endpoint Name";
    }
}