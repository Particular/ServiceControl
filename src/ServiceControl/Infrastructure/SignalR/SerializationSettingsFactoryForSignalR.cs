namespace ServiceControl.Infrastructure.SignalR
{
    using Newtonsoft.Json;
    using ServiceBus.Management.Infrastructure.Nancy;

    public static class SerializationSettingsFactoryForSignalR
    {
        public static JsonSerializerSettings CreateDefault()
        {
            var s = JsonNetSerializer.CreateDefault();
            s.ContractResolver = new CustomSignalRContractResolverBecauseOfIssue500InSignalR();
            return s;
        }
    }
}