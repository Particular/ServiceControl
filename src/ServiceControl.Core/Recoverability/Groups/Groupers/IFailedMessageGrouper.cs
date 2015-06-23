namespace ServiceControl.Recoverability.Groups.Groupers
{
    using Particular.Operations.Ingestion.Api;
    using ServiceControl.Contracts.Operations;

    public interface IFailedMessageGrouper
    {
        string GroupType { get; }
        string GetGroupName(IngestedMessage actualMessage, FailureDetails failureDetails);
    }
}