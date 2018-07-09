namespace ServiceBus.Management.AcceptanceTests
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Pipeline;

    internal class StampDispatchBehavior : IBehavior<IDispatchContext, IDispatchContext>
    {
        private ScenarioContext scenarioContext;

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
    }
}