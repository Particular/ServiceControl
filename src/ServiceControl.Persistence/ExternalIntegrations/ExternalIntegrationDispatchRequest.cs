namespace ServiceControl.ExternalIntegrations
{
    public class ExternalIntegrationDispatchRequest
    {
        public string? Id { get; set; }
        public required object DispatchContext;
    }
}