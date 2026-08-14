namespace ServiceControl.ExternalIntegrations
{
    public class ExternalIntegrationDispatchRequest
    {
        public required string Id { get; set; }
        public required object DispatchContext;
    }
}