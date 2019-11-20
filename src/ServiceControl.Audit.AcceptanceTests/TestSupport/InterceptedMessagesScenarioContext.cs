namespace ServiceControl.Audit.AcceptanceTests.TestSupport
{
    using System.Collections.Concurrent;
    using Contracts.EndpointControl;
    using Contracts.MessageFailures;
    using NServiceBus.AcceptanceTesting;

    public class InterceptedMessagesScenarioContext : ScenarioContext
    {
        public ConcurrentBag<RegisterNewEndpoint> SentRegisterEndpointCommands { get; } = new ConcurrentBag<RegisterNewEndpoint>();
        public ConcurrentBag<MarkMessageFailureResolvedByRetry> SentMarkMessageFailureResolvedByRetriesCommands { get; } = new ConcurrentBag<MarkMessageFailureResolvedByRetry>();
    }
}