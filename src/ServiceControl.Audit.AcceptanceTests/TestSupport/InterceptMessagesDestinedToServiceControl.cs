namespace ServiceBus.Management.AcceptanceTests
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Pipeline;
    using ServiceControl.Contracts.EndpointControl;
    using ServiceControl.Contracts.MessageFailures;

    class InterceptMessagesDestinedToServiceControl : Behavior<IOutgoingLogicalMessageContext>
    {
        readonly ScenarioContext scenarioContext;

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
    }
}