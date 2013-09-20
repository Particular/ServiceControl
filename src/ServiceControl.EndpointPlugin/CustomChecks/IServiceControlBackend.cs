namespace ServiceControl.EndpointPlugin.CustomChecks
{
    public interface IServiceControlBackend
    {
        void Send(ReportCustomCheck reportCustomCheck);
    }
}