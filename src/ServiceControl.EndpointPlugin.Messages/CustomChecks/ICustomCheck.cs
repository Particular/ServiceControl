namespace ServiceControl.EndpointPlugin.Messages.CustomChecks
{
    // needed for DI
    public interface ICustomCheck
    {
        void ReportOk();
        void ReportFailed(string failureReason);
    }
}
