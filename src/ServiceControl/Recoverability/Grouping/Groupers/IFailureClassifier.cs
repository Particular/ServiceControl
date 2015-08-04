namespace ServiceControl.Recoverability
{
    using ServiceControl.Contracts.Operations;

    interface IFailureClassifier
    {
        string Name { get; }
        string ClassifyFailure(FailureDetails failureDetails);
    }
}