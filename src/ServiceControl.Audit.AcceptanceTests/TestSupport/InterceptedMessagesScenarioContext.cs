namespace ServiceBus.Management.AcceptanceTests
{
    using System.Collections.Concurrent;
    using NServiceBus.AcceptanceTesting;
    using ServiceControl.Contracts.EndpointControl;
    using ServiceControl.Contracts.MessageFailures;

    public class InterceptedMessagesScenarioContext : ScenarioContext
    {
        public ConcurrentBag<RegisterNewEndpoint> SentRegisterEndpointCommands { get; } = new ConcurrentBag<RegisterNewEndpoint>();
        public ConcurrentBag<MarkMessageFailureResolvedByRetry> SentMarkMessageFailureResolvedByRetriesCommands { get; } = new ConcurrentBag<MarkMessageFailureResolvedByRetry>();
    }
}