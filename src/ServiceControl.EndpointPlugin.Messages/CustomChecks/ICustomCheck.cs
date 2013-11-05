namespace ServiceControl.EndpointPlugin.Messages.CustomChecks
{
    // needed for DI
    public interface ICustomCheck
    {
        string Category { get; }
        string Id { get; }
        void ReportOk();
        void ReportFailed(string failureReason);
    }
}
