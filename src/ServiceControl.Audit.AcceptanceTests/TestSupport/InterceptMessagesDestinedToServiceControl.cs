namespace ServiceControl.Audit.AcceptanceTests.TestSupport
{
    using System;
    using System.Threading.Tasks;
    using Contracts.EndpointControl;
    using Contracts.MessageFailures;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Pipeline;

    class InterceptMessagesDestinedToServiceControl : Behavior<IOutgoingLogicalMessageContext>
    {
        public InterceptMessagesDestinedToServiceControl(ScenarioContext context)
        {
            scenarioContext = context;
        }

        public override Task Invoke(IOutgoingLogicalMessageContext context, Func<Task> next)
        {
            var interceptingContext = scenarioContext as InterceptedMessagesScenarioContext;
            switch (context.Message.Instance)
            {
                case RegisterNewEndpoint newEndpoint:
                    interceptingContext?.SentRegisterEndpointCommands.Add(newEndpoint);
                    return Task.CompletedTask;
                case MarkMessageFailureResolvedByRetry markMessageFailureResolvedByRetry:
                    interceptingContext?.SentMarkMessageFailureResolvedByRetriesCommands.Add(markMessageFailureResolvedByRetry);
                    return Task.CompletedTask;
                default:
                    return next();
            }
        }

        readonly ScenarioContext scenarioContext;
    }
}