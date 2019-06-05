namespace ServiceBus.Management.AcceptanceTests
{
    using System.Collections.Generic;
    using System.Net.Http;
    using Newtonsoft.Json;

    interface IAcceptanceTestInfrastructureProvider
    {
        Dictionary<string, HttpClient> HttpClients { get; }

        JsonSerializerSettings SerializerSettings { get; }

        Dictionary<string, dynamic> SettingsPerInstance { get; }
        Dictionary<string, OwinHttpMessageHandler> Handlers { get; }
        Dictionary<string, dynamic> Busses { get; }
    }
}