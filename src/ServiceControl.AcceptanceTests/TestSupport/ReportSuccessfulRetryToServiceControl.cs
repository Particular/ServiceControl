namespace ServiceBus.Management.AcceptanceTests
{
    using System;
    using System.Threading.Tasks;
    using Infrastructure.Settings;
    using NServiceBus;
    using NServiceBus.Pipeline;
    using ServiceControl.Contracts.MessageFailures;

    class ReportSuccessfulRetryToServiceControl : IBehavior<IIncomingPhysicalMessageContext, IIncomingPhysicalMessageContext>
    {
        public async Task Invoke(IIncomingPhysicalMessageContext context, Func<IIncomingPhysicalMessageContext, Task> next)
        {
            await next(context).ConfigureAwait(false);

            if (context.MessageHeaders.TryGetValue("ServiceControl.Retry.UniqueMessageId", out var messageId))
            {
                var options = new SendOptions();

                options.DoNotEnforceBestPractices();
                options.SetDestination(Settings.DEFAULT_SERVICE_NAME);

                await context.Send(new MessageFailureResolvedByRetry
                {
                    FailedMessageId = messageId
                }, options).ConfigureAwait(false);
            }
        }
    }
}