namespace ServiceBus.Management.AcceptanceTests
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Pipeline;
    using ServiceControl.Contracts.MessageFailures;

    class ReportSuccessfulRetryToServiceControl : Behavior<IIncomingPhysicalMessageContext>
    {
        public async override Task Invoke(IIncomingPhysicalMessageContext context, Func<Task> next)
        {
            await next();

            if (context.MessageHeaders.TryGetValue("ServiceControl.Retry.UniqueMessageId", out var messageId))
            {
                var options = new SendOptions();

                options.DoNotEnforceBestPractices();
                options.SetDestination(Infrastructure.Settings.Settings.DEFAULT_SERVICE_NAME);

                await context.Send(new MessageFailureResolvedByRetry
                {
                    FailedMessageId = messageId
                },options);
            }
        }
    }
}