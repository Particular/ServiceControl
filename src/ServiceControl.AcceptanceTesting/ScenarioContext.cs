namespace NServiceBus.AcceptanceTesting
{
    public abstract class ScenarioContext
    {
        public bool EndpointsStarted { get; set; }
        public string Exceptions { get; set; }
        public string SessionId { get; set; }
        public bool HasNativePubSubSupport { get; set; }
    }
}