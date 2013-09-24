namespace ServiceControl.EndpointPlugin.Messages.CustomChecks
{
    // needed for DI
    public interface ICustomCheck
    {
        string Category { get; }
        string CustomCheckId { get; }
        void ReportOk();
        void ReportFailed(string failureReason);
    }
}
