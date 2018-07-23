namespace ServiceBus.Management.AcceptanceTests
{
    using System.Collections.Generic;
    using System.Net.Http;
    using Newtonsoft.Json;
    using Infrastructure;
    using Infrastructure.Settings;

    public interface IAcceptanceTestInfrastructureProvider
    {
        Dictionary<string, HttpClient> HttpClients { get; }
        
        JsonSerializerSettings SerializerSettings { get; }
        
        Dictionary<string, Settings> SettingsPerInstance { get; }
        Dictionary<string, OwinHttpMessageHandler> Handlers { get; }
        Dictionary<string, BusInstance> Busses { get; }
    }
}