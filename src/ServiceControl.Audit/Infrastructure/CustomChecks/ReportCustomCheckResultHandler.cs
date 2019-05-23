namespace ServiceControl.CustomChecks
{
    using System.Threading.Tasks;
    using NServiceBus;
    using Plugin.CustomChecks.Messages;

    class ReportCustomCheckResultHandler : IHandleMessages<ReportCustomCheckResult>
    {
        public Task Handle(ReportCustomCheckResult message, IMessageHandlerContext context)
        {
            var options = new PublishOptions();
            options.DoNotEnforceBestPractices();

            return context.Publish(message, options);
        }
    }
}