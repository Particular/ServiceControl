namespace ServiceControl.AcceptanceTests.TestSupport
{
    using System;
    using System.Threading.Tasks;
    using Contracts.MessageFailures;
    using NServiceBus;
    using NServiceBus.Pipeline;
    using ServiceBus.Management.Infrastructure.Settings;

    class ReportSuccessfulRetryToServiceControl : IBehavior<IIncomingPhysicalMessageContext, IIncomingPhysicalMessageContext>
    {
        public async Task Invoke(IIncomingPhysicalMessageContext context, Func<IIncomingPhysicalMessageContext, Task> next)
        {
            await next(context);

            if (context.MessageHeaders.TryGetValue("ServiceControl.Retry.UniqueMessageId", out var messageId))
            {
                var options = new SendOptions();
                options.SetDestination(PrimaryOptions.DEFAULT_INSTANCE_NAME);

                await context.Send(new MarkMessageFailureResolvedByRetry
                {
                    FailedMessageId = messageId
                }, options);
            }
        }
    }
}