namespace ServiceControl.Recoverability
{
    public class EndpointInstanceClassifier : IFailureClassifier
    {
        public const string Id = "Endpoint Instance";
        public string Name => Id;

        public string ClassifyFailure(ClassifiableMessageDetails failureDetails)
        {
            if (failureDetails.ProcessingAttempt == null)
            {
                return null;
            }

            var instanceId = EndpointInstanceId.From(failureDetails.ProcessingAttempt.Headers);
            return instanceId.InstanceId;
        }
    }
}