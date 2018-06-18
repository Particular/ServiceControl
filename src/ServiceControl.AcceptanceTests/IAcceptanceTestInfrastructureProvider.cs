namespace ServiceBus.Management.AcceptanceTests
{
    using System.Collections.Generic;
    using System.Net.Http;
    using Newtonsoft.Json;
    using ServiceBus.Management.Infrastructure.Settings;

    public interface IAcceptanceTestInfrastructureProvider
    {
        Dictionary<string, HttpClient> HttpClients { get; }
        
        JsonSerializerSettings SerializerSettings { get; }
        
        Dictionary<string, Settings> SettingsPerInstance { get; }
        Dictionary<string, OwinHttpMessageHandler> Handlers { get; }
    }
}