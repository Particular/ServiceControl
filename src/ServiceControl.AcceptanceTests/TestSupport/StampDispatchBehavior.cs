namespace ServiceBus.Management.AcceptanceTests
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Pipeline;

    class StampDispatchBehavior : IBehavior<IDispatchContext, IDispatchContext>
    {
        public StampDispatchBehavior(ScenarioContext scenarioContext)
        {
            this.scenarioContext = scenarioContext;
        }

        public Task Invoke(IDispatchContext context, Func<IDispatchContext, Task> next)
        {
            foreach (var operation in context.Operations)
            {
                operation.Message.Headers["SC.SessionID"] = scenarioContext.TestRunId.ToString();
            }

            return next(context);
        }

        ScenarioContext scenarioContext;
    }
}