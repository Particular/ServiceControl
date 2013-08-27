namespace ServiceControl.BusinessMonitoring
{
    using Contracts.Operations;
    using NServiceBus;

    public class EndpointPerformanceDataReceivedHandler : IHandleMessages<EndpointPerformanceDataReceived>
    {
        public EndpointSLAMonitoring EndpointSLAMonitoring { get; set; }

        public void Handle(EndpointPerformanceDataReceived message)
        {
            if (message.Data.ContainsKey("CriticalTime"))
            {
                EndpointSLAMonitoring.ReportCriticalTimeMeasurements(message.Endpoint, message.Data["CriticalTime"]);
            }
        }
    }
}