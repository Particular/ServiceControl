namespace ServiceControl.Infrastructure.SignalR
{
    using Newtonsoft.Json;

    public static class SerializationSettingsFactoryForSignalR
    {
        public static JsonSerializerSettings CreateDefault()
        {
            var s = ServiceBus.Management.Infrastructure.Nancy.JsonNetSerializer.CreateDefault();
            s.ContractResolver = new CustomSignalRContractResolverBecauseOfIssue500InSignalR();
            return s;
        }
    }
}