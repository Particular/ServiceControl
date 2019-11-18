namespace ServiceBus.Management.AcceptanceTests
{
    using System.Net.Http;
    using Infrastructure;
    using Newtonsoft.Json;

    interface IAcceptanceTestInfrastructureProvider
    {
        HttpClient HttpClient { get; }

        JsonSerializerSettings SerializerSettings { get; }

        OwinHttpMessageHandler Handler { get; }
        BusInstance Bus { get; }
        string Port { get; }
    }
}