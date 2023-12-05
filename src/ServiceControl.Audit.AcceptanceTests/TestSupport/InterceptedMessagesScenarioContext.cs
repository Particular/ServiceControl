namespace ServiceControl.Audit.AcceptanceTests.TestSupport
{
    using System.Collections.Concurrent;
    using Contracts.EndpointControl;
    using Contracts.MessageFailures;
    using NServiceBus.AcceptanceTesting;

    public class InterceptedMessagesScenarioContext : ScenarioContext
    {
        public ConcurrentBag<RegisterNewEndpoint> SentRegisterEndpointCommands { get; } = [];
        public ConcurrentBag<MarkMessageFailureResolvedByRetry> SentMarkMessageFailureResolvedByRetriesCommands { get; } = [];
    }
}