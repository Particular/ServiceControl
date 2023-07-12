namespace ServiceControl.ExternalIntegrations
{
    public class ExternalIntegrationDispatchRequest
    {
        public string Id { get; set; }
        public object DispatchContext;   // TODO: This is of type object, do we want the persister API to explicitly do something with this? Maybe instead of object use the specific types? Alternatively already have SC do serialization on this field so the storage engine does not need to?
    }
}