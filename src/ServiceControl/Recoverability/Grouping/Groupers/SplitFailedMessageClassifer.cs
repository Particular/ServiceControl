namespace ServiceControl.Recoverability
{
    using NServiceBus;
    using ServiceControl.MessageFailures;

    public class SplitFailedMessageClassifer : IFailureClassifier
    {
        public string Name => "Split Failure";
        public string ClassifyFailure(ClassifiableMessageDetails failureDetails)
        {
            throw new System.NotImplementedException();
        }

        public bool ApplyToNewFailures => false;

        public string ClassifyFailure(string messageType, FailedMessageStatus originalStatus, FailedMessage.ProcessingAttempt attempt)
        {
            var endpointName = attempt.Headers.ContainsKey(Headers.OriginatingEndpoint) ? attempt.Headers[Headers.OriginatingEndpoint] : attempt.FailureDetails.AddressOfFailingEndpoint;
            var classification = $"{endpointName}/{messageType}/{originalStatus}";

            return classification;
        }
    }

}