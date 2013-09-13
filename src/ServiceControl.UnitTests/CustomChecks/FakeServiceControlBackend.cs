namespace ServiceControl.UnitTests
{
    using System.Collections.Generic;
    using EndpointPlugin.CustomChecks;

    public class FakeServiceControlBackend : IServiceControlBackend
    {
        public List<ReportCustomCheck> ReportedChecks = new List<ReportCustomCheck>();

        public void Send(ReportCustomCheck reportCustomCheck)
        {
            ReportedChecks.Add(reportCustomCheck);
        }
    }
}