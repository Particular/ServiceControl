namespace ServiceControl.Recoverability
{
    public class AddressOfFailingEndpointClassifier : IFailureClassifier
    {
        public const string Id = "Endpoint Address";
        public string Name => Id;

        public string ClassifyFailure(ClassifiableMessageDetails failureDetails)
        {
            return failureDetails.Details?.AddressOfFailingEndpoint;
        }
    }
}