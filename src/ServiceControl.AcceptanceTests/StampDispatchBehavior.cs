namespace ServiceBus.Management.AcceptanceTests
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus.Pipeline;

    class StampDispatchBehavior : IBehavior<IDispatchContext, IDispatchContext>
    {
        public Task Invoke(IDispatchContext context, Func<IDispatchContext, Task> next)
        {
            foreach (var operation in context.Operations)
            {
                operation.Message.Headers["SC.SessionID"] = StaticLoggerFactory.CurrentContext.TestRunId.ToString();
            }

            return next(context);
        }
    }
}