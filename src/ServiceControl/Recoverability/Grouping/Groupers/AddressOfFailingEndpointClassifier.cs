namespace ServiceControl.Recoverability
{
    public class AddressOfFailingEndpointClassifier : IFailureClassifier
    {
        public string Name => Id;

        public string ClassifyFailure(ClassifiableMessageDetails failureDetails)
        {
            return failureDetails.Details?.AddressOfFailingEndpoint;
        }

        public const string Id = "Endpoint Address";
    }
}