namespace ServiceControl.EndpointPlugin.CustomChecks.Internal
{
    // needed for DI
    public interface ICustomCheck
    {
        void ReportOk();
        void ReportFailed(string failureReason);
    }
}
