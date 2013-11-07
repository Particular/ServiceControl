namespace ServiceControl.CustomChecks
{
    using EndpointPlugin.Messages.CustomChecks;
    using NServiceBus;

    class CustomCheckResultHandler : IHandleMessages<ReportCustomCheckResult>
    {
        public CustomCheckMonitor Monitor { get; set; }

        public IBus Bus { get; set; }

        public void Handle(ReportCustomCheckResult message)
        {
            Monitor.RegisterResult(message, Bus.CurrentMessageContext.Headers);
        }
    }
}
