namespace ServiceControl.Recoverability
{
    public interface IFailureClassifier
    {
        string Name { get; }
        string ClassifyFailure(ClassifiableMessageDetails failureDetails);
        bool ApplyToNewFailures { get; }
    }
}