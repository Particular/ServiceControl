namespace ServiceControl.Infrastructure.SignalR
{
    using Newtonsoft.Json;
    using WebApi;

    static class SerializationSettingsFactoryForSignalR
    {
        public static JsonSerializerSettings CreateDefault()
        {
            var s = JsonNetSerializerSettings.CreateDefault();
            s.ContractResolver = new CustomSignalRContractResolverBecauseOfIssue500InSignalR();
            return s;
        }
    }
}