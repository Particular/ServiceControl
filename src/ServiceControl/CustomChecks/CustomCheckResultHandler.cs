namespace ServiceControl.CustomChecks
{
    using NServiceBus;
    using Plugin.CustomChecks.Messages;

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
