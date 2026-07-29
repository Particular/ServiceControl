namespace ServiceControl.Audit.AcceptanceTests.TestSupport
{
    using System.Collections.Concurrent;
    using Contracts.EndpointControl;
    using NServiceBus.AcceptanceTesting;

    public class InterceptedMessagesScenarioContext : ScenarioContext
    {
        public ConcurrentBag<RegisterNewEndpoint> SentRegisterEndpointCommands { get; } = [];
    }
}