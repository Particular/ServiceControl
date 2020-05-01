namespace ServiceControl.Recoverability
{
    using System.Collections.Generic;
    using Contracts.Operations;

    class EndpointInstanceClassifier : IFailureClassifier
    {
        public string Name => Id;

        public string ClassifyFailure(ClassifiableMessageDetails failureDetails)
        {
            return failureDetails.ProcessingAttempt == null ? null : ExtractInstanceId(failureDetails.ProcessingAttempt.Headers);
        }

        static string ExtractInstanceId(IReadOnlyDictionary<string, string> headers)
        {
            if (headers.TryGetValue("NServiceBus.Metric.InstanceId", out var instanceId))
            {
                return instanceId;
            }

            var details = EndpointDetailsParser.ReceivingEndpoint(headers);
            return details?.HostId.ToString("N");
        }

        const string Id = "Endpoint Instance";
    }
}